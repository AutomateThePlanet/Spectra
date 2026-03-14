using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;

namespace Spectra.MCP.Tests.Execution;

public class DependencyResolverTests
{
    private readonly DependencyResolver _resolver = new();

    [Fact]
    public void PropagateBlocks_DirectDependency_BlocksDependent()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        var blocked = _resolver.PropagateBlocks(queue, "TC-001");

        Assert.Contains("TC-002", blocked);
    }

    [Fact]
    public void PropagateBlocks_TransitiveDependency_BlocksAllDownstream()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" },
            new TestIndexEntry { Id = "TC-003", File = "tc-003.md", Title = "Test 3", Priority = "low", DependsOn = "TC-002" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        var blocked = _resolver.PropagateBlocks(queue, "TC-001");

        Assert.Contains("TC-002", blocked);
        Assert.Contains("TC-003", blocked);
    }

    [Fact]
    public void PropagateBlocks_NoDependents_ReturnsEmpty()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        var blocked = _resolver.PropagateBlocks(queue, "TC-001");

        Assert.Empty(blocked);
    }

    [Fact]
    public void PropagateBlocks_AlreadyBlocked_NotIncluded()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" }
        };

        var queue = TestQueue.Build("run-1", testEntries);

        // Block TC-002 first
        queue.MarkBlocked("TC-002", "TC-001");

        var blocked = _resolver.PropagateBlocks(queue, "TC-001");

        // TC-002 was already blocked, so shouldn't appear in new blocks
        Assert.Empty(blocked);
    }

    [Fact]
    public void AreDependenciesSatisfied_NoDependency_ReturnsTrue()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        var satisfied = _resolver.AreDependenciesSatisfied(queue, "TC-001");

        Assert.True(satisfied);
    }

    [Fact]
    public void AreDependenciesSatisfied_DependencyPassed_ReturnsTrue()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        queue.MarkCompleted(queue.GetById("TC-001")!.TestHandle, TestStatus.Passed);

        var satisfied = _resolver.AreDependenciesSatisfied(queue, "TC-002");

        Assert.True(satisfied);
    }

    [Fact]
    public void AreDependenciesSatisfied_DependencyPending_ReturnsFalse()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        var satisfied = _resolver.AreDependenciesSatisfied(queue, "TC-002");

        Assert.False(satisfied);
    }

    [Fact]
    public void AreDependenciesSatisfied_DependencyFailed_ReturnsFalse()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" }
        };

        var queue = TestQueue.Build("run-1", testEntries);
        queue.MarkCompleted(queue.GetById("TC-001")!.TestHandle, TestStatus.Failed);

        var satisfied = _resolver.AreDependenciesSatisfied(queue, "TC-002");

        Assert.False(satisfied);
    }

    [Fact]
    public void GetBlockChain_ReturnsFullChain()
    {
        var testEntries = new[]
        {
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test 1", Priority = "high" },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "medium", DependsOn = "TC-001" },
            new TestIndexEntry { Id = "TC-003", File = "tc-003.md", Title = "Test 3", Priority = "low", DependsOn = "TC-002" }
        };

        var queue = TestQueue.Build("run-1", testEntries);

        // Simulate: TC-001 fails, causing TC-002 and TC-003 to be blocked
        queue.MarkCompleted(queue.GetById("TC-001")!.TestHandle, TestStatus.Failed);
        queue.MarkBlocked("TC-002", "TC-001");
        queue.MarkBlocked("TC-003", "TC-002");

        var chain = _resolver.GetBlockChain(queue, "TC-003");

        // The chain includes TC-002 (blocked) and TC-001 (failed) - the root cause
        Assert.True(chain.Count >= 1);
        Assert.Contains("TC-001", chain);
    }
}
