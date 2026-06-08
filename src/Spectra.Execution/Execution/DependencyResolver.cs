using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Execution;

/// <summary>
/// Resolves and propagates dependency blocks transitively.
/// </summary>
public sealed class DependencyResolver
{
    /// <summary>
    /// Propagates blocks transitively from a failed/skipped/blocked test.
    /// Returns all test IDs that were blocked.
    /// </summary>
    public IReadOnlyList<string> PropagateBlocks(TestQueue queue, string failedTestId)
    {
        var allBlocked = new List<string>();
        var toProcess = new Queue<string>();
        toProcess.Enqueue(failedTestId);

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            if (processed.Contains(currentId)) continue;
            processed.Add(currentId);

            // Block all tests that depend on this one
            var blocked = queue.BlockDependents(currentId);
            foreach (var blockedId in blocked)
            {
                allBlocked.Add(blockedId);
                // Also process these for transitive blocking
                toProcess.Enqueue(blockedId);
            }
        }

        return allBlocked;
    }

    /// <summary>
    /// Checks if a test's dependencies are satisfied.
    /// </summary>
    public bool AreDependenciesSatisfied(TestQueue queue, string testId)
    {
        var test = queue.GetById(testId);
        if (test is null || string.IsNullOrEmpty(test.DependsOn))
        {
            return true;
        }

        var dependency = queue.GetById(test.DependsOn);
        return dependency is not null && dependency.Status == TestStatus.Passed;
    }

    /// <summary>
    /// Gets the chain of blocking tests for a blocked test.
    /// </summary>
    public IReadOnlyList<string> GetBlockChain(TestQueue queue, string blockedTestId)
    {
        var chain = new List<string>();
        var current = queue.GetById(blockedTestId);

        while (current is not null && !string.IsNullOrEmpty(current.DependsOn))
        {
            var dependency = queue.GetById(current.DependsOn);
            if (dependency is null) break;

            if (StateMachine.IsTerminal(dependency.Status) && dependency.Status != TestStatus.Passed)
            {
                chain.Add(dependency.TestId);
            }

            current = dependency;
        }

        return chain;
    }
}
