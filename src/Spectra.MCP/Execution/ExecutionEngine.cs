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
    private readonly IUserIdentityResolver _identity;
    private readonly McpConfig _config;
    private readonly DependencyResolver _dependencyResolver;

    // In-memory queue state per run
    private readonly Dictionary<string, TestQueue> _queues = [];

    public ExecutionEngine(
        RunRepository runRepo,
        ResultRepository resultRepo,
        IUserIdentityResolver identity,
        McpConfig config)
    {
        _runRepo = runRepo;
        _resultRepo = resultRepo;
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

        if (!_queues.TryGetValue(runId, out var queue))
        {
            return null;
        }

        return (run, queue);
    }

    /// <summary>
    /// Gets the test queue for a run, loading from DB if needed.
    /// </summary>
    public async Task<TestQueue?> GetQueueAsync(string runId)
    {
        if (_queues.TryGetValue(runId, out var queue))
        {
            return queue;
        }

        // Reconstruct from database
        var results = await _resultRepo.GetByRunIdAsync(runId);
        if (results.Count == 0)
        {
            return null;
        }

        queue = new TestQueue(runId);
        // Queue reconstruction from results would require index data
        // For now, return null if not in memory
        return null;
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

        if (_queues.TryGetValue(runId, out var queue))
        {
            queue.MarkInProgress(testHandle);
        }

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

        if (_queues.TryGetValue(runId, out var queue))
        {
            queue.MarkCompleted(testHandle, status);

            // Handle blocking if failed or skipped
            if (status is TestStatus.Failed or TestStatus.Skipped)
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
    /// Skips a test with a reason.
    /// </summary>
    public async Task<(TestResult Skipped, IReadOnlyList<string> Blocked, QueuedTest? Next)> SkipTestAsync(
        string runId,
        string testHandle,
        string reason)
    {
        return await AdvanceTestAsync(runId, testHandle, TestStatus.Skipped, reason);
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

        if (!force && _queues.TryGetValue(runId, out var queue) && queue.PendingCount > 0)
        {
            throw new InvalidOperationException($"{queue.PendingCount} tests are still pending. Use force=true to finalize anyway.");
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

        if (!_queues.TryGetValue(runId, out var queue))
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
}
