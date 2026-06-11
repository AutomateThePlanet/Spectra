using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Engine;

/// <summary>
/// Spec 070 — ported from the MCP <c>CancelExecutionRunTests</c> + <c>CancelAllActiveRunsToolTests</c>.
/// Cancel transitions a run to Cancelled and removes it from the active set; cancel-all (GAP: no prior
/// engine/CLI coverage) is the engine composition <c>GetActiveRunsAsync()</c> → <c>CancelRunAsync</c> per
/// run, which the retired tool performed.
/// </summary>
public class CancelEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly McpConfig _config;

    public CancelEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _config = new McpConfig { BasePath = _testDir };
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private ExecutionEngine Engine(string user = "user-a") =>
        new(_runRepo, _resultRepo, new QueueSnapshotRepository(_db), new MockUserIdentityResolver(user), _config);

    private static List<TestIndexEntry> Entries() =>
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "One", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Two", Priority = "medium" }
    ];

    [Fact]
    public async Task Cancel_TransitionsToCancelled_AndLeavesActiveSet()
    {
        var engine = Engine();
        var (run, _) = await engine.StartRunAsync("checkout", Entries());

        var cancelled = await engine.CancelRunAsync(run.RunId, "no longer needed");

        Assert.NotNull(cancelled);
        Assert.Equal(RunStatus.Cancelled, cancelled!.Status);
        Assert.DoesNotContain(await _runRepo.GetActiveRunsAsync(), r => r.RunId == run.RunId);
    }

    [Fact]
    public async Task Cancel_UnknownRun_ReturnsNull()
    {
        var engine = Engine();
        Assert.Null(await engine.CancelRunAsync("does-not-exist"));
    }

    [Fact]
    public async Task CancelAll_CancelsEveryActiveRun()
    {
        var engine = Engine();
        // One user can hold multiple active runs across different suites.
        await engine.StartRunAsync("checkout", Entries());
        await engine.StartRunAsync("auth", Entries());

        var active = await _runRepo.GetActiveRunsAsync();
        Assert.Equal(2, active.Count);

        // cancel-all == the retired tool's composition over the engine.
        var cancelledCount = 0;
        foreach (var run in active)
        {
            if (await engine.CancelRunAsync(run.RunId, "bulk cancel") is not null) cancelledCount++;
        }

        Assert.Equal(2, cancelledCount);
        Assert.Empty(await _runRepo.GetActiveRunsAsync());
    }

    private sealed class MockUserIdentityResolver : IUserIdentityResolver
    {
        private readonly string _user;
        public MockUserIdentityResolver(string user) => _user = user;
        public string GetCurrentUser() => _user;
    }
}
