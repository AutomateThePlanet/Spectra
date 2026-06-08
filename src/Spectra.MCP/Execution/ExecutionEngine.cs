using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Execution;

/// <summary>
/// Orchestrates test execution runs.
/// </summary>
public sealed class ExecutionEngine
{
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly QueueSnapshotRepository _snapshotRepo;
    private readonly IUserIdentityResolver _identity;
    private readonly McpConfig _config;
    private readonly DependencyResolver _dependencyResolver;

    // In-memory queue state per run
    private readonly Dictionary<string, TestQueue> _queues = [];

    public ExecutionEngine(
        RunRepository runRepo,
        ResultRepository resultRepo,
        QueueSnapshotRepository snapshotRepo,
        IUserIdentityResolver identity,
        McpConfig config)
    {
        _runRepo = runRepo;
        _resultRepo = resultRepo;
        _snapshotRepo = snapshotRepo;
        _identity = identity;
        _config = config;
        _dependencyResolver = new DependencyResolver();
    }

    /// <summary>
    /// Starts a new execution run.
    /// </summary>
    public async Task<(Run Run, TestQueue Queue)> StartRunAsync(
        string suite,
        IEnumerable<TestIndexEntry> testEntries,
        string? environment = null,
        RunFilters? filters = null)
    {
        var user = _identity.GetCurrentUser();

        // Check for existing active run
        var existingRun = await _runRepo.GetActiveRunAsync(suite, user);
        if (existingRun is not null)
        {
            throw new InvalidOperationException($"Active run exists for suite '{suite}' by user '{user}'");
        }

        var runId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        // Build the test queue
        var queue = TestQueue.Build(runId, testEntries, filters);
        if (queue.TotalCount == 0)
        {
            throw new InvalidOperationException("No tests match the specified filters");
        }

        // Create run record
        var run = new Run
        {
            RunId = runId,
            Suite = suite,
            Status = RunStatus.Running,
            StartedAt = now,
            StartedBy = user,
            Environment = environment,
            Filters = filters,
            UpdatedAt = now
        };

        await _runRepo.CreateAsync(run);

        // Persist the orchestration snapshot (spec 064) so the queue is reconstructable from the
        // DB alone, independent of the mutable on-disk index. If this fails, the exception
        // propagates and run creation fails — never leave an unreconstructable run (FR-007).
        var snapshot = queue.Tests.Select((t, index) => new QueueSnapshotEntry
        {
            RunId = runId,
            TestId = t.TestId,
            Title = t.Title,
            Priority = t.Priority.ToString().ToLowerInvariant(),
            DependsOn = t.DependsOn,
            OrderIndex = index
        });

        await _snapshotRepo.CreateManyAsync(snapshot);

        // Create test result records
        var results = queue.Tests.Select(t => new TestResult
        {
            RunId = runId,
            TestId = t.TestId,
            TestHandle = t.TestHandle,
            Status = TestStatus.Pending,
            Attempt = 1
        });

        await _resultRepo.CreateManyAsync(results);

        // Store queue in memory
        _queues[runId] = queue;

        return (run, queue);
    }

    /// <summary>
    /// Gets a run by ID.
    /// </summary>
    public async Task<Run?> GetRunAsync(string runId)
    {
        return await _runRepo.GetByIdAsync(runId);
    }

    /// <summary>
    /// Gets run and queue status.
    /// </summary>
    public async Task<(Run Run, TestQueue Queue)?> GetStatusAsync(string runId)
    {
        var run = await _runRepo.GetByIdAsync(runId);
        if (run is null) return null;

        // Reconstruct the queue from the DB when not held in memory, so status (and callers like
        // retest) behave identically in a short-lived process (FR-004). Fails loud on a corrupt
        // snapshot; returns null only when the run genuinely has nothing recorded.
        var queue = await GetQueueAsync(runId);
        if (queue is null) return null;

        return (run, queue);
    }

    /// <summary>
    /// Gets the test queue for a run, loading from DB if needed.
    /// </summary>
    public async Task<TestQueue?> GetQueueAsync(string runId)
    {
        // Warm path: the original in-memory queue is authoritative when present (FR-005).
        if (_queues.TryGetValue(runId, out var queue))
        {
            return queue;
        }

        // Cold path: reconstruct losslessly from the durable orchestration snapshot + results.
        var results = await _resultRepo.GetByRunIdAsync(runId);
        var snapshot = await _snapshotRepo.GetByRunIdAsync(runId);

        // Benign absence — the run genuinely has nothing recorded (run not found). Not an error.
        if (results.Count == 0 && snapshot.Count == 0)
        {
            return null;
        }

        // Faithful rebuild or fail loud (FR-001/FR-003).
        queue = ReconstructQueue(runId, results, snapshot);
        _queues[runId] = queue;
        return queue;
    }

    /// <summary>
    /// Reconstructs a TestQueue losslessly from the durable orchestration snapshot (spec 064),
    /// using the latest-attempt result per test for current status and handle. Fails loud rather
    /// than silently producing a degraded queue when the snapshot cannot faithfully rebuild it.
    /// </summary>
    private static TestQueue ReconstructQueue(
        string runId,
        IReadOnlyList<TestResult> results,
        IReadOnlyList<QueueSnapshotEntry> snapshot)
    {
        // Latest attempt per test_id (handles retest — newest handle/status wins).
        var latestByTestId = results
            .GroupBy(r => r.TestId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.Attempt).First(),
                StringComparer.Ordinal);

        // Fail-loud: results exist but no orchestration snapshot to rebuild from.
        if (snapshot.Count == 0)
        {
            throw new QueueReconstructionException(runId,
                $"Run '{runId}' has recorded results but no orchestration snapshot; the execution queue cannot be faithfully reconstructed.");
        }

        var snapshotIds = new HashSet<string>(snapshot.Select(s => s.TestId), StringComparer.Ordinal);

        // Fail-loud: a recorded result has no snapshot row (orchestration data missing).
        foreach (var testId in latestByTestId.Keys)
        {
            if (!snapshotIds.Contains(testId))
            {
                throw new QueueReconstructionException(runId,
                    $"Run '{runId}' has a recorded result for test '{testId}' with no orchestration snapshot row.");
            }
        }

        // Fail-loud: a snapshot row has no recorded result (incomplete capture).
        foreach (var entry in snapshot)
        {
            if (!latestByTestId.ContainsKey(entry.TestId))
            {
                throw new QueueReconstructionException(runId,
                    $"Run '{runId}' orchestration snapshot references test '{entry.TestId}' with no recorded result.");
            }
        }

        // Fail-loud: a dependency edge points at a test absent from the snapshot.
        foreach (var entry in snapshot)
        {
            if (!string.IsNullOrEmpty(entry.DependsOn) && !snapshotIds.Contains(entry.DependsOn))
            {
                throw new QueueReconstructionException(runId,
                    $"Run '{runId}' snapshot test '{entry.TestId}' depends on '{entry.DependsOn}', which is absent from the snapshot.");
            }
        }

        // Build the queue in the original order (snapshot already ordered by order_index).
        var queue = new TestQueue(runId);
        foreach (var entry in snapshot)
        {
            var latest = latestByTestId[entry.TestId];
            queue.AddReconstructed(entry, latest.Status, latest.TestHandle);
        }

        return queue;
    }

    /// <summary>
    /// Gets a test result by handle.
    /// </summary>
    public async Task<TestResult?> GetTestResultAsync(string testHandle)
    {
        return await _resultRepo.GetByHandleAsync(testHandle);
    }

    /// <summary>
    /// Marks a test as in progress.
    /// </summary>
    public async Task<TestResult?> StartTestAsync(string runId, string testHandle)
    {
        var result = await _resultRepo.GetByHandleAsync(testHandle);
        if (result is null) return null;

        var transition = StateMachine.Transition(result, TestStatus.InProgress);
        if (!transition.IsSuccess) return null;

        await _resultRepo.UpdateStatusAsync(
            testHandle,
            TestStatus.InProgress,
            startedAt: DateTime.UtcNow);

        var queue = await GetQueueAsync(runId);
        queue?.MarkInProgress(testHandle);

        return transition.Value;
    }

    /// <summary>
    /// Records a test result and returns the next test.
    /// </summary>
    public async Task<(TestResult Recorded, IReadOnlyList<string> Blocked, QueuedTest? Next)> AdvanceTestAsync(
        string runId,
        string testHandle,
        TestStatus status,
        string? notes = null)
    {
        var result = await _resultRepo.GetByHandleAsync(testHandle);
        if (result is null)
        {
            throw new InvalidOperationException($"Test handle not found: {testHandle}");
        }

        var transition = StateMachine.Transition(result, status);
        if (!transition.IsSuccess)
        {
            throw new InvalidOperationException(transition.ErrorMessage);
        }

        await _resultRepo.UpdateStatusAsync(
            testHandle,
            status,
            completedAt: DateTime.UtcNow,
            notes: notes);

        var blocked = new List<string>();
        QueuedTest? next = null;

        // Reconstruct from the DB when not held in memory so block-propagation and next-test
        // selection are identical across process boundaries (FR-004).
        var queue = await GetQueueAsync(runId);
        if (queue is not null)
        {
            queue.MarkCompleted(testHandle, status);

            // Handle blocking if failed, skipped, or blocked
            if (status is TestStatus.Failed or TestStatus.Skipped or TestStatus.Blocked)
            {
                var testId = TestHandle.ExtractTestId(testHandle);
                if (testId is not null)
                {
                    blocked = _dependencyResolver.PropagateBlocks(queue, testId).ToList();

                    // Update blocked tests in DB
                    foreach (var blockedId in blocked)
                    {
                        var blockedTest = queue.GetById(blockedId);
                        if (blockedTest is not null)
                        {
                            await _resultRepo.UpdateStatusAsync(
                                blockedTest.TestHandle,
                                TestStatus.Blocked,
                                completedAt: DateTime.UtcNow,
                                blockedBy: testId);
                        }
                    }
                }
            }

            next = queue.GetNext();
        }

        return (transition.Value!, blocked, next);
    }

    /// <summary>
    /// Skips a test with a reason, optionally marking it as blocked.
    /// </summary>
    public async Task<(TestResult Skipped, IReadOnlyList<string> Blocked, QueuedTest? Next)> SkipTestAsync(
        string runId,
        string testHandle,
        string reason,
        bool blocked = false)
    {
        var status = blocked ? TestStatus.Blocked : TestStatus.Skipped;
        return await AdvanceTestAsync(runId, testHandle, status, reason);
    }

    /// <summary>
    /// Adds a note to a test without changing status.
    /// </summary>
    public async Task AddNoteAsync(string testHandle, string note)
    {
        await _resultRepo.AppendNoteAsync(testHandle, note);
    }

    /// <summary>
    /// Pauses a run.
    /// </summary>
    public async Task<Run?> PauseRunAsync(string runId)
    {
        var run = await _runRepo.GetByIdAsync(runId);
        if (run is null) return null;

        var transition = StateMachine.Transition(run, RunStatus.Paused);
        if (!transition.IsSuccess) return null;

        await _runRepo.UpdateStatusAsync(runId, RunStatus.Paused);
        return transition.Value;
    }

    /// <summary>
    /// Resumes a paused run.
    /// </summary>
    public async Task<Run?> ResumeRunAsync(string runId)
    {
        var run = await _runRepo.GetByIdAsync(runId);
        if (run is null) return null;

        var transition = StateMachine.Transition(run, RunStatus.Running);
        if (!transition.IsSuccess) return null;

        await _runRepo.UpdateStatusAsync(runId, RunStatus.Running);
        return transition.Value;
    }

    /// <summary>
    /// Cancels a run.
    /// </summary>
    public async Task<Run?> CancelRunAsync(string runId, string? reason = null)
    {
        var run = await _runRepo.GetByIdAsync(runId);
        if (run is null) return null;

        var transition = StateMachine.Transition(run, RunStatus.Cancelled);
        if (!transition.IsSuccess) return null;

        await _runRepo.UpdateStatusAsync(runId, RunStatus.Cancelled, DateTime.UtcNow);
        _queues.Remove(runId);
        return transition.Value;
    }

    /// <summary>
    /// Finalizes a run and generates reports.
    /// </summary>
    public async Task<Run?> FinalizeRunAsync(string runId, bool force = false)
    {
        var run = await _runRepo.GetByIdAsync(runId);
        if (run is null) return null;

        if (!force)
        {
            // Reconstruct from the DB when not held in memory so the pending guard is honoured
            // regardless of process lifetime (FR-004).
            var queue = await GetQueueAsync(runId);
            if (queue is not null && queue.PendingCount > 0)
            {
                throw new InvalidOperationException($"{queue.PendingCount} tests are still pending. Use force=true to finalize anyway.");
            }
        }

        var transition = StateMachine.Transition(run, RunStatus.Completed);
        if (!transition.IsSuccess) return null;

        await _runRepo.UpdateStatusAsync(runId, RunStatus.Completed, DateTime.UtcNow);
        _queues.Remove(runId);
        return transition.Value;
    }

    /// <summary>
    /// Gets status counts for a run.
    /// </summary>
    public async Task<Dictionary<TestStatus, int>> GetStatusCountsAsync(string runId)
    {
        return await _resultRepo.GetStatusCountsAsync(runId);
    }

    /// <summary>
    /// Gets all results for a run.
    /// </summary>
    public async Task<IReadOnlyList<TestResult>> GetResultsAsync(string runId)
    {
        return await _resultRepo.GetByRunIdAsync(runId);
    }

    /// <summary>
    /// Requeues a test for another attempt.
    /// </summary>
    public async Task<TestResult?> RetestAsync(string runId, string testId)
    {
        var latestAttempt = await _resultRepo.GetLatestAttemptAsync(runId, testId);
        if (latestAttempt == 0) return null;

        var newAttempt = latestAttempt + 1;

        // Reconstruct from the DB when not held in memory — fixes the cross-process RUN_NOT_FOUND
        // bug where retest hard-failed after a restart (FR-004).
        var queue = await GetQueueAsync(runId);
        if (queue is null)
        {
            return null;
        }

        var requeued = queue.Requeue(testId, newAttempt);
        if (requeued is null) return null;

        var result = new TestResult
        {
            RunId = runId,
            TestId = testId,
            TestHandle = requeued.TestHandle,
            Status = TestStatus.Pending,
            Attempt = newAttempt
        };

        await _resultRepo.CreateAsync(result);
        return result;
    }

    /// <summary>
    /// Verifies a user owns a run.
    /// </summary>
    public async Task<bool> VerifyOwnerAsync(string runId)
    {
        var run = await _runRepo.GetByIdAsync(runId);
        if (run is null) return false;

        var currentUser = _identity.GetCurrentUser();
        return run.StartedBy.Equals(currentUser, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the active run for the current user.
    /// </summary>
    public async Task<Run?> GetActiveRunAsync()
    {
        var user = _identity.GetCurrentUser();
        return await _runRepo.GetActiveRunByUserAsync(user);
    }

    /// <summary>
    /// Records results for multiple tests in bulk.
    /// </summary>
    public async Task<BulkRecordResult> BulkRecordResultsAsync(
        string runId,
        IEnumerable<string> testHandles,
        TestStatus status,
        string? notes = null)
    {
        var processedTests = new List<TestResult>();
        var allBlocked = new List<string>();
        var now = DateTime.UtcNow;

        // Reconstruct from the DB when not held in memory so block-propagation is identical across
        // process boundaries (FR-004). Fetched once for the whole batch.
        var queue = await GetQueueAsync(runId);

        foreach (var testHandle in testHandles)
        {
            var result = await _resultRepo.GetByHandleAsync(testHandle);
            if (result is null) continue;

            // Only process pending or in-progress tests
            if (result.Status != TestStatus.Pending && result.Status != TestStatus.InProgress)
            {
                continue;
            }

            // For pending tests, mark as started first
            if (result.Status == TestStatus.Pending)
            {
                await _resultRepo.UpdateStatusAsync(testHandle, TestStatus.InProgress, startedAt: now);
            }

            // Update to final status
            await _resultRepo.UpdateStatusAsync(
                testHandle,
                status,
                completedAt: now,
                notes: notes);

            // Update queue state
            if (queue is not null)
            {
                queue.MarkCompleted(testHandle, status);

                // Handle blocking for failed/skipped/blocked tests
                if (status is TestStatus.Failed or TestStatus.Skipped or TestStatus.Blocked)
                {
                    var testId = TestHandle.ExtractTestId(testHandle);
                    if (testId is not null)
                    {
                        var blocked = _dependencyResolver.PropagateBlocks(queue, testId);
                        foreach (var blockedId in blocked)
                        {
                            var blockedTest = queue.GetById(blockedId);
                            if (blockedTest is not null)
                            {
                                await _resultRepo.UpdateStatusAsync(
                                    blockedTest.TestHandle,
                                    TestStatus.Blocked,
                                    completedAt: now,
                                    blockedBy: testId);
                            }
                        }
                        allBlocked.AddRange(blocked);
                    }
                }
            }

            processedTests.Add(new TestResult
            {
                RunId = runId,
                TestId = result.TestId,
                TestHandle = testHandle,
                Status = status,
                Notes = notes,
                CompletedAt = now,
                Attempt = result.Attempt
            });
        }

        return new BulkRecordResult
        {
            ProcessedCount = processedTests.Count,
            ProcessedTests = processedTests,
            BlockedTests = allBlocked.Distinct().ToList()
        };
    }
}

/// <summary>
/// Result of a bulk record operation.
/// </summary>
public sealed class BulkRecordResult
{
    public int ProcessedCount { get; init; }
    public IReadOnlyList<TestResult> ProcessedTests { get; init; } = [];
    public IReadOnlyList<string> BlockedTests { get; init; } = [];
}
