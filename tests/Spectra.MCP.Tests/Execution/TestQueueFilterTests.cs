using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;

namespace Spectra.MCP.Tests.Execution;

public class TestQueueFilterTests
{
    private readonly List<TestIndexEntry> _testEntries =
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Login High", Priority = "high", Tags = ["smoke", "auth"], Component = "auth" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Login Medium", Priority = "medium", Tags = ["regression", "auth"], Component = "auth" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "Checkout High", Priority = "high", Tags = ["smoke", "checkout"], Component = "checkout" },
        new() { Id = "TC-004", File = "tc-004.md", Title = "Checkout Low", Priority = "low", Tags = ["regression"], Component = "checkout" },
        new() { Id = "TC-005", File = "tc-005.md", Title = "Payment Medium", Priority = "medium", Tags = ["smoke", "payment"], Component = "payment" }
    ];

    [Fact]
    public void Build_NoFilters_IncludesAllTests()
    {
        var queue = TestQueue.Build("run-1", _testEntries);

        Assert.Equal(5, queue.TotalCount);
    }

    [Fact]
    public void Build_FilterByPriorityHigh_OnlyHighPriority()
    {
        var filters = new RunFilters { Priority = Priority.High };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(2, queue.TotalCount);
        Assert.All(queue.Tests, t => Assert.Equal(Priority.High, t.Priority));
    }

    [Fact]
    public void Build_FilterByPriorityMedium_OnlyMediumPriority()
    {
        var filters = new RunFilters { Priority = Priority.Medium };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(2, queue.TotalCount);
        Assert.All(queue.Tests, t => Assert.Equal(Priority.Medium, t.Priority));
    }

    [Fact]
    public void Build_FilterByPriorityLow_OnlyLowPriority()
    {
        var filters = new RunFilters { Priority = Priority.Low };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Single(queue.Tests);
        Assert.Equal("TC-004", queue.Tests[0].TestId);
    }

    [Fact]
    public void Build_FilterBySingleTag_MatchesTag()
    {
        var filters = new RunFilters { Tags = ["smoke"] };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        // TC-001, TC-003, TC-005 have the smoke tag
        Assert.Equal(3, queue.TotalCount);
        Assert.Contains(queue.Tests, t => t.TestId == "TC-001");
        Assert.Contains(queue.Tests, t => t.TestId == "TC-003");
        Assert.Contains(queue.Tests, t => t.TestId == "TC-005");
    }

    [Fact]
    public void Build_FilterByMultipleTags_RequiresAllTags()
    {
        var filters = new RunFilters { Tags = ["smoke", "auth"] };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Single(queue.Tests);
        Assert.Equal("TC-001", queue.Tests[0].TestId);
    }

    [Fact]
    public void Build_FilterByComponent_OnlyMatchingComponent()
    {
        var filters = new RunFilters { Component = "checkout" };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(2, queue.TotalCount);
        Assert.All(queue.Tests, t => Assert.Contains("Checkout", t.Title));
    }

    [Fact]
    public void Build_FilterByTestIds_OnlySpecifiedTests()
    {
        var filters = new RunFilters { TestIds = ["TC-001", "TC-003"] };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(2, queue.TotalCount);
        Assert.Contains(queue.Tests, t => t.TestId == "TC-001");
        Assert.Contains(queue.Tests, t => t.TestId == "TC-003");
    }

    [Fact]
    public void Build_FilterByTestIds_IncludesDependencies()
    {
        var entriesWithDeps = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Base Test", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Depends on 1", Priority = "medium", DependsOn = "TC-001" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Depends on 2", Priority = "low", DependsOn = "TC-002" }
        };

        // Request only TC-003, but should include TC-001 and TC-002 as dependencies
        var filters = new RunFilters { TestIds = ["TC-003"] };
        var queue = TestQueue.Build("run-1", entriesWithDeps, filters);

        Assert.Equal(3, queue.TotalCount);
        Assert.Contains(queue.Tests, t => t.TestId == "TC-001");
        Assert.Contains(queue.Tests, t => t.TestId == "TC-002");
        Assert.Contains(queue.Tests, t => t.TestId == "TC-003");
    }

    [Fact]
    public void Build_CombinedFilters_AppliesAllFilters()
    {
        var filters = new RunFilters
        {
            Priority = Priority.High,
            Tags = ["smoke"]
        };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(2, queue.TotalCount);
        Assert.All(queue.Tests, t =>
        {
            Assert.Equal(Priority.High, t.Priority);
        });
    }

    [Fact]
    public void Build_NoMatchingTests_ReturnsEmptyQueue()
    {
        var filters = new RunFilters { Tags = ["nonexistent"] };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(0, queue.TotalCount);
    }

    [Fact]
    public void Build_FiltersCaseInsensitive()
    {
        var filters = new RunFilters { Component = "CHECKOUT" };
        var queue = TestQueue.Build("run-1", _testEntries, filters);

        Assert.Equal(2, queue.TotalCount);
    }

    [Fact]
    public void Build_PriorityFilterCaseInsensitive()
    {
        var entriesWithMixedCase = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test", Priority = "HIGH" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test 2", Priority = "High" }
        };

        var filters = new RunFilters { Priority = Priority.High };
        var queue = TestQueue.Build("run-1", entriesWithMixedCase, filters);

        Assert.Equal(2, queue.TotalCount);
    }
}
