using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Integration;

/// <summary>
/// US4 (spec 064): the engine's advance/skip/retest/finalize flow behaves identically when driven by
/// a fresh (short-lived) engine that reconstructs the queue from the DB, including the two paths that
/// previously degraded across a process boundary — retest (hard-failed RUN_NOT_FOUND) and finalize's
/// pending guard (silently skipped).
/// </summary>
public class ReconstructedExecutionFlowTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly McpConfig _config;

    private readonly List<TestIndexEntry> _entries =
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Login", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Checkout", Priority = "medium" }
    ];

    public ReconstructedExecutionFlowTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _db = new ExecutionDb(_testDir);
        _config = new McpConfig { BasePath = _testDir };
    }

    private ExecutionEngine NewEngine() => new(
        new RunRepository(_db), new ResultRepository(_db), new QueueSnapshotRepository(_db),
        new UserIdentityResolver(), _config);

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Retest_AcrossProcessBoundary_NoLongerReturnsNull()
    {
        var origin = NewEngine();
        var (run, queue) = await origin.StartRunAsync("suite", _entries);
        var tc001 = queue.Tests.First(t => t.TestId == "TC-001").TestHandle;
        await origin.StartTestAsync(run.RunId, tc001);
        await origin.AdvanceTestAsync(run.RunId, tc001, TestStatus.Failed, "broke");

        // Fresh engine (cold _queues) — previously this returned null (RUN_NOT_FOUND surface).
        var retest = await NewEngine().RetestAsync(run.RunId, "TC-001");

        Assert.NotNull(retest);
        Assert.Equal(2, retest!.Attempt);
        Assert.Equal(TestStatus.Pending, retest.Status);
    }

    [Fact]
    public async Task Finalize_WithoutForce_AcrossProcessBoundary_HonoursPendingGuard()
    {
        var (run, _) = await NewEngine().StartRunAsync("suite", _entries);

        // Fresh engine, all tests still pending — the guard must fire (previously silently skipped).
        var fresh = NewEngine();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fresh.FinalizeRunAsync(run.RunId, force: false));
    }

    [Fact]
    public async Task Finalize_WithForce_AcrossProcessBoundary_Completes()
    {
        var (run, _) = await NewEngine().StartRunAsync("suite", _entries);

        var finalized = await NewEngine().FinalizeRunAsync(run.RunId, force: true);

        Assert.NotNull(finalized);
        Assert.Equal(RunStatus.Completed, finalized!.Status);
    }

    [Fact]
    public async Task Advance_AcrossProcessBoundary_ReturnsCorrectNextTest()
    {
        var origin = NewEngine();
        var (run, queue) = await origin.StartRunAsync("suite", _entries);
        var tc001 = queue.Tests.First(t => t.TestId == "TC-001").TestHandle;

        var fresh = NewEngine();
        await fresh.StartTestAsync(run.RunId, tc001);
        var (_, _, next) = await fresh.AdvanceTestAsync(run.RunId, tc001, TestStatus.Passed);

        Assert.Equal("TC-002", next?.TestId);
    }
}
