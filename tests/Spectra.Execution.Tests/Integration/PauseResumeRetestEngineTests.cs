using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Integration;

/// <summary>
/// Spec 070 — ported from the MCP <c>PauseResumeTests</c> and <c>RetestFlowTests</c> to drive
/// <see cref="ExecutionEngine"/> directly. Pause/resume preserve progress and the queue position (resume
/// continues at the next pending test); retest requeues a completed test as a fresh pending attempt.
/// Cross-process retest (no <c>RUN_NOT_FOUND</c>) is additionally covered by
/// <c>Spectra.CLI.Tests/Commands/Run/ParityTests.Retest_AcrossFreshServices_RequeuesLikeEngine</c>.
/// </summary>
public class PauseResumeRetestEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;

    public PauseResumeRetestEngineTests()
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

    private static List<TestIndexEntry> Three() =>
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "One", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Two", Priority = "high" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "Three", Priority = "high" }
    ];

    [Fact]
    public async Task PauseResume_PreservesProgress_AndResumesAtNextTest()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Three());
        var first = queue.GetNext()!;
        await _engine.StartTestAsync(run.RunId, first.TestHandle);
        await _engine.AdvanceTestAsync(run.RunId, first.TestHandle, TestStatus.Passed, null);

        var paused = await _engine.PauseRunAsync(run.RunId);
        Assert.Equal(RunStatus.Paused, paused!.Status);
        Assert.Equal(1, (await _engine.GetStatusCountsAsync(run.RunId)).GetValueOrDefault(TestStatus.Passed));

        var resumed = await _engine.ResumeRunAsync(run.RunId);
        Assert.Equal(RunStatus.Running, resumed!.Status);

        var next = (await _engine.GetQueueAsync(run.RunId))!.GetNext();
        Assert.Equal("TC-002", next!.TestId);
    }

    [Fact]
    public async Task MultiplePauseResume_KeepsStateConsistent()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Three());

        await _engine.PauseRunAsync(run.RunId);
        Assert.Equal(RunStatus.Running, (await _engine.ResumeRunAsync(run.RunId))!.Status);

        var first = (await _engine.GetQueueAsync(run.RunId))!.GetNext()!;
        await _engine.StartTestAsync(run.RunId, first.TestHandle);
        await _engine.AdvanceTestAsync(run.RunId, first.TestHandle, TestStatus.Passed, null);

        Assert.Equal(RunStatus.Paused, (await _engine.PauseRunAsync(run.RunId))!.Status);
        Assert.Equal(RunStatus.Running, (await _engine.ResumeRunAsync(run.RunId))!.Status);
        Assert.Equal("TC-002", (await _engine.GetQueueAsync(run.RunId))!.GetNext()!.TestId);
    }

    [Fact]
    public async Task Retest_RequeuesCompletedTest_AsFreshPendingAttempt()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", Three());
        var first = queue.GetNext()!;
        await _engine.StartTestAsync(run.RunId, first.TestHandle);
        await _engine.AdvanceTestAsync(run.RunId, first.TestHandle, TestStatus.Failed, "flaky");

        var requeued = await _engine.RetestAsync(run.RunId, "TC-001");

        Assert.NotNull(requeued);
        Assert.Equal(TestStatus.Pending, requeued!.Status);
        Assert.Equal(2, requeued.Attempt);
    }
}
