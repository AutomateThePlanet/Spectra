using Spectra.Core.Models;
using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Execution;

/// <summary>
/// Manages the ordered queue of tests for execution.
/// </summary>
public sealed class TestQueue
{
    private readonly List<QueuedTest> _tests = [];
    private int _currentIndex;

    public string RunId { get; }
    public IReadOnlyList<QueuedTest> Tests => _tests.AsReadOnly();
    public int CurrentIndex => _currentIndex;
    public int TotalCount => _tests.Count;
    public int CompletedCount => _tests.Count(t => StateMachine.IsTerminal(t.Status));
    public int PendingCount => _tests.Count(t => t.Status == TestStatus.Pending);

    public TestQueue(string runId)
    {
        RunId = runId;
    }

    /// <summary>
    /// Builds the execution queue from index entries with optional filters.
    /// </summary>
    public static TestQueue Build(string runId, IEnumerable<TestIndexEntry> entries, RunFilters? filters = null)
    {
        var queue = new TestQueue(runId);
        var filtered = ApplyFilters(entries, filters);
        var ordered = OrderByDependencies(filtered);

        foreach (var entry in ordered)
        {
            var handle = TestHandle.Generate(runId, entry.Id);
            queue._tests.Add(new QueuedTest
            {
                TestId = entry.Id,
                TestHandle = handle,
                Title = entry.Title,
                Priority = ParsePriority(entry.Priority),
                DependsOn = entry.DependsOn,
                Status = TestStatus.Pending
            });
        }

        return queue;
    }

    /// <summary>
    /// Gets the next pending test, or null if none remain.
    /// </summary>
    public QueuedTest? GetNext()
    {
        for (var i = _currentIndex; i < _tests.Count; i++)
        {
            var test = _tests[i];
            if (test.Status == TestStatus.Pending)
            {
                return test;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a test by handle.
    /// </summary>
    public QueuedTest? GetByHandle(string testHandle)
    {
        return _tests.FirstOrDefault(t => t.TestHandle == testHandle);
    }

    /// <summary>
    /// Gets a test by ID.
    /// </summary>
    public QueuedTest? GetById(string testId)
    {
        return _tests.FirstOrDefault(t => t.TestId == testId);
    }

    /// <summary>
    /// Marks a test as in progress.
    /// </summary>
    public void MarkInProgress(string testHandle)
    {
        var index = _tests.FindIndex(t => t.TestHandle == testHandle);
        if (index >= 0)
        {
            _tests[index] = _tests[index] with { Status = TestStatus.InProgress };
            _currentIndex = index;
        }
    }

    /// <summary>
    /// Marks a test as completed with the given status.
    /// </summary>
    public void MarkCompleted(string testHandle, TestStatus status)
    {
        var index = _tests.FindIndex(t => t.TestHandle == testHandle);
        if (index >= 0)
        {
            _tests[index] = _tests[index] with { Status = status };
            // Move to next test
            if (index == _currentIndex)
            {
                _currentIndex++;
            }
        }
    }

    /// <summary>
    /// Marks a specific test as blocked by another test.
    /// </summary>
    public void MarkBlocked(string testId, string blockedBy)
    {
        var index = _tests.FindIndex(t => t.TestId == testId);
        if (index >= 0 && _tests[index].Status == TestStatus.Pending)
        {
            _tests[index] = _tests[index] with { Status = TestStatus.Blocked };
        }
    }

    /// <summary>
    /// Blocks tests that depend on the given test.
    /// </summary>
    public IReadOnlyList<string> BlockDependents(string testId)
    {
        var blocked = new List<string>();

        for (var i = 0; i < _tests.Count; i++)
        {
            var test = _tests[i];
            if (test.DependsOn == testId && test.Status == TestStatus.Pending)
            {
                _tests[i] = test with { Status = TestStatus.Blocked };
                blocked.Add(test.TestId);
            }
        }

        return blocked;
    }

    /// <summary>
    /// Re-queues a test for another attempt.
    /// </summary>
    public QueuedTest? Requeue(string testId, int newAttempt)
    {
        var existingIndex = _tests.FindIndex(t => t.TestId == testId);
        if (existingIndex < 0) return null;

        var existing = _tests[existingIndex];
        var newHandle = TestHandle.Generate(RunId, testId);

        var requeued = new QueuedTest
        {
            TestId = testId,
            TestHandle = newHandle,
            Title = existing.Title,
            Priority = existing.Priority,
            DependsOn = existing.DependsOn,
            Status = TestStatus.Pending
        };

        _tests.Add(requeued);
        return requeued;
    }

    /// <summary>
    /// Gets the current progress as "completed/total".
    /// </summary>
    public string GetProgress()
    {
        return $"{CompletedCount}/{TotalCount}";
    }

    private static IEnumerable<TestIndexEntry> ApplyFilters(IEnumerable<TestIndexEntry> entries, RunFilters? filters)
    {
        if (filters is null || !filters.HasFilters)
        {
            return entries;
        }

        var filtered = entries.AsEnumerable();

        if (filters.Priority.HasValue)
        {
            var priorityStr = filters.Priority.Value.ToString().ToLowerInvariant();
            filtered = filtered.Where(e => e.Priority.Equals(priorityStr, StringComparison.OrdinalIgnoreCase));
        }

        if (filters.Tags?.Count > 0)
        {
            // AND logic: test must have ALL specified tags
            filtered = filtered.Where(e =>
                filters.Tags.All(tag => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrEmpty(filters.Component))
        {
            filtered = filtered.Where(e =>
                filters.Component.Equals(e.Component, StringComparison.OrdinalIgnoreCase));
        }

        if (filters.TestIds?.Count > 0)
        {
            // Include specified tests AND their dependencies
            var testIdSet = new HashSet<string>(filters.TestIds, StringComparer.OrdinalIgnoreCase);
            var entriesList = entries.ToList();

            // Add dependencies recursively
            var toProcess = new Queue<string>(filters.TestIds);
            while (toProcess.Count > 0)
            {
                var id = toProcess.Dequeue();
                var entry = entriesList.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (entry?.DependsOn is not null && !testIdSet.Contains(entry.DependsOn))
                {
                    testIdSet.Add(entry.DependsOn);
                    toProcess.Enqueue(entry.DependsOn);
                }
            }

            filtered = filtered.Where(e => testIdSet.Contains(e.Id));
        }

        return filtered;
    }

    private static IEnumerable<TestIndexEntry> OrderByDependencies(IEnumerable<TestIndexEntry> entries)
    {
        var entryList = entries.ToList();
        var entryMap = entryList.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<TestIndexEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(TestIndexEntry entry)
        {
            if (visited.Contains(entry.Id)) return;

            // Visit dependency first if it exists
            if (!string.IsNullOrEmpty(entry.DependsOn) && entryMap.TryGetValue(entry.DependsOn, out var dep))
            {
                Visit(dep);
            }

            visited.Add(entry.Id);
            ordered.Add(entry);
        }

        // Sort by priority first, then by ID for determinism
        var sortedEntries = entryList
            .OrderBy(e => GetPriorityOrder(e.Priority))
            .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in sortedEntries)
        {
            Visit(entry);
        }

        return ordered;
    }

    private static int GetPriorityOrder(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "high" => 0,
            "medium" => 1,
            "low" => 2,
            _ => 3
        };
    }

    private static Priority ParsePriority(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "high" => Priority.High,
            "medium" => Priority.Medium,
            "low" => Priority.Low,
            _ => Priority.Medium
        };
    }
}
