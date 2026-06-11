using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Engine;

/// <summary>
/// Spec 070 — ported from the MCP <c>FinalizeExecutionRunTests</c>, asserting the engine's finalize
/// semantics: the pending guard blocks finalize without <c>force</c> and <c>force:true</c> completes the
/// run. Report-file generation is a CLI/handler concern (covered by <c>RunLoopSmokeTests</c>) and the
/// report content by the relocated <c>ReportGenerator</c>/<c>ReportWriter</c> tests.
/// </summary>
public class FinalizeEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;

    public FinalizeEngineTests()
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

    private static List<TestIndexEntry> Entries() =>
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "One", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Two", Priority = "medium" }
    ];

    [Fact]
    public async Task Finalize_WithPendingTests_Throws_WithoutForce()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", Entries());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _engine.FinalizeRunAsync(run.RunId, force: false));
        Assert.Contains("pending", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Finalize_Force_CompletesRun()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", Entries());

        var completed = await _engine.FinalizeRunAsync(run.RunId, force: true);

        Assert.NotNull(completed);
        Assert.Equal(RunStatus.Completed, completed!.Status);
        Assert.Null(await _engine.GetActiveRunAsync());
    }

    [Fact]
    public async Task Finalize_AllResolved_CompletesWithoutForce()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Entries());
        foreach (var t in queue.Tests.ToList())
        {
            await _engine.StartTestAsync(run.RunId, t.TestHandle);
            await _engine.AdvanceTestAsync(run.RunId, t.TestHandle, TestStatus.Passed, null);
        }

        var completed = await _engine.FinalizeRunAsync(run.RunId, force: false);
        Assert.Equal(RunStatus.Completed, completed!.Status);
    }
}
