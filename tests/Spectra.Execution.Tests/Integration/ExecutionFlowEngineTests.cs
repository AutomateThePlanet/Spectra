using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Integration;

/// <summary>
/// Spec 070 — ported from the MCP <c>ExecutionFlowTests</c>, <c>BlockingCascadeTests</c>, and
/// <c>FilteredExecutionTests</c> to drive <see cref="ExecutionEngine"/> directly. Covers the full
/// start→advance→finalize loop end-to-end, multi-level dependency-block cascade, and filter application
/// at <c>StartRunAsync</c> (priority/tags/component/test-ids/combined, plus the no-match guard). Per-filter
/// matching is also unit-covered by the relocated <c>TestQueueFilterTests</c>; this asserts the engine
/// integrates filters into the run.
/// </summary>
public class ExecutionFlowEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;

    public ExecutionFlowEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _db = new ExecutionDb(_testDir);
        _engine = new ExecutionEngine(new RunRepository(_db), new ResultRepository(_db),
            new QueueSnapshotRepository(_db), new UserIdentityResolver(), new McpConfig { BasePath = _testDir });
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static List<TestIndexEntry> Mixed() =>
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Login High Smoke", Priority = "high", Tags = ["smoke", "auth"], Component = "auth" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Login Medium", Priority = "medium", Tags = ["regression"], Component = "auth" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "Checkout High Smoke", Priority = "high", Tags = ["smoke"], Component = "checkout" },
        new() { Id = "TC-004", File = "tc-004.md", Title = "Payment Low", Priority = "low", Tags = ["regression"], Component = "payment" }
    ];

    [Fact]
    public async Task FullFlow_StartAdvanceAll_ThenFinalizeWithoutForce()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Mixed());
        foreach (var t in queue.Tests.ToList())
        {
            await _engine.StartTestAsync(run.RunId, t.TestHandle);
            await _engine.AdvanceTestAsync(run.RunId, t.TestHandle, TestStatus.Passed, null);
        }

        var completed = await _engine.FinalizeRunAsync(run.RunId, force: false);
        Assert.Equal(RunStatus.Completed, completed!.Status);

        var counts = await _engine.GetStatusCountsAsync(run.RunId);
        Assert.Equal(4, counts.GetValueOrDefault(TestStatus.Passed));
    }

    [Fact]
    public async Task BlockingCascade_FailingRoot_BlocksWholeDependentChain()
    {
        var chain = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Root", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Mid", Priority = "high", DependsOn = "TC-001" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Leaf", Priority = "high", DependsOn = "TC-002" }
        };
        var (run, queue) = await _engine.StartRunAsync("checkout", chain);
        var root = queue.GetById("TC-001")!;
        await _engine.StartTestAsync(run.RunId, root.TestHandle);

        var (_, blocked, _) = await _engine.AdvanceTestAsync(run.RunId, root.TestHandle, TestStatus.Failed, "root broke");

        Assert.Contains("TC-002", blocked);
        var rows = await _engine.GetResultsAsync(run.RunId);
        Assert.Equal(TestStatus.Blocked, rows.First(r => r.TestId == "TC-002").Status);
        Assert.Equal(TestStatus.Blocked, rows.First(r => r.TestId == "TC-003").Status);
    }

    [Theory]
    [InlineData("priority", 2)]
    [InlineData("tags", 2)]
    [InlineData("component", 2)]
    [InlineData("testids", 2)]
    [InlineData("combined", 2)]
    public async Task StartRun_AppliesFilters_ToQueueSize(string kind, int expected)
    {
        var filters = kind switch
        {
            "priority" => new RunFilters { Priorities = ["high"] },
            "tags" => new RunFilters { Tags = ["smoke"] },
            "component" => new RunFilters { Components = ["auth"] },
            "testids" => new RunFilters { TestIds = ["TC-001", "TC-004"] },
            "combined" => new RunFilters { Priorities = ["high"], Tags = ["smoke"] },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var (_, queue) = await _engine.StartRunAsync("checkout", Mixed(), filters: filters);
        Assert.Equal(expected, queue.TotalCount);
    }

    [Fact]
    public async Task StartRun_NoMatchingTests_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _engine.StartRunAsync("checkout", Mixed(), filters: new RunFilters { Tags = ["nonexistent"] }));
        Assert.Contains("No tests match", ex.Message);
    }
}
