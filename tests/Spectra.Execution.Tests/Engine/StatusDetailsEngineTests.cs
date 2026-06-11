using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Engine;

/// <summary>
/// Spec 070 — ported from the MCP <c>GetExecutionStatusTests</c> + <c>GetTestCaseDetailsTests</c>.
/// Status is the engine composition <c>GetRunAsync</c> + <c>GetStatusCountsAsync</c> + <c>GetQueueAsync</c>
/// (run state, per-status counts, remaining queue); details is <c>GetTestResultAsync</c> resolving a handle
/// to its row. The JSON envelope the tools wrapped around these is transport and is retired.
/// </summary>
public class StatusDetailsEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;

    public StatusDetailsEngineTests()
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
    public async Task Status_ReflectsRunStateCountsAndRemainingQueue()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Entries());
        var first = queue.GetNext()!;
        await _engine.StartTestAsync(run.RunId, first.TestHandle);
        await _engine.AdvanceTestAsync(run.RunId, first.TestHandle, TestStatus.Passed, null);

        var status = await _engine.GetStatusAsync(run.RunId);
        Assert.NotNull(status);
        Assert.Equal(RunStatus.Running, status!.Value.Run.Status);

        var counts = await _engine.GetStatusCountsAsync(run.RunId);
        Assert.Equal(1, counts.GetValueOrDefault(TestStatus.Passed));

        var remaining = await _engine.GetQueueAsync(run.RunId);
        Assert.Equal(1, remaining!.PendingCount);
    }

    [Fact]
    public async Task Details_ResolvesHandleToItsRow()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Entries());
        var first = queue.GetNext()!;
        await _engine.StartTestAsync(run.RunId, first.TestHandle);

        var result = await _engine.GetTestResultAsync(first.TestHandle);
        Assert.NotNull(result);
        Assert.Equal(first.TestId, result!.TestId);
        Assert.Equal(run.RunId, result.RunId);
    }
}
