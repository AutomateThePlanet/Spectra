using Spectra.Core.Models;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Integration;

/// <summary>
/// Spec 070 — ported from the MCP <c>ConcurrentUsersTests</c> to call <see cref="ExecutionEngine"/>
/// directly (the transport wrapper is removed). Verifies the engine's per-user active-run isolation:
/// the same user cannot hold two active runs for the same suite (the engine throws
/// "Active run exists …", which the retired MCP tool mapped to <c>ACTIVE_RUN_EXISTS</c>), but different
/// users, different suites, and a freed slot (cancel/finalize) all allow a fresh start.
/// </summary>
public class ConcurrentUsersTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly McpConfig _config;
    private readonly List<TestIndexEntry> _testEntries;

    public ConcurrentUsersTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _config = new McpConfig { BasePath = _testDir, ReportsPath = Path.Combine(_testDir, "reports") };

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium" }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private ExecutionEngine Engine(string user) =>
        new(_runRepo, _resultRepo, new QueueSnapshotRepository(_db), new MockUserIdentityResolver(user), _config);

    [Fact]
    public async Task DifferentUsers_SameSuite_BothCanStart()
    {
        var engineA = Engine("user-a");
        var engineB = Engine("user-b");

        var (runA, _) = await engineA.StartRunAsync("checkout", _testEntries);
        var (runB, _) = await engineB.StartRunAsync("checkout", _testEntries);

        Assert.NotNull(runA.RunId);
        Assert.NotNull(runB.RunId);
        Assert.NotEqual(runA.RunId, runB.RunId);
    }

    [Fact]
    public async Task SameUser_SameSuite_SecondStartFails()
    {
        var engine = Engine("user-a");

        await engine.StartRunAsync("checkout", _testEntries);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.StartRunAsync("checkout", _testEntries));
        Assert.Contains("Active run exists", ex.Message);
    }

    [Fact]
    public async Task SameUser_DifferentSuites_BothCanStart()
    {
        var engine = Engine("user-a");

        var (run1, _) = await engine.StartRunAsync("checkout", _testEntries);
        var (run2, _) = await engine.StartRunAsync("auth", _testEntries);

        Assert.NotEqual(run1.RunId, run2.RunId);
    }

    [Fact]
    public async Task SameUser_AfterCancel_CanStartNew()
    {
        var engine = Engine("user-a");

        var (run1, _) = await engine.StartRunAsync("checkout", _testEntries);
        await engine.CancelRunAsync(run1.RunId);

        var (run2, _) = await engine.StartRunAsync("checkout", _testEntries);
        Assert.NotEqual(run1.RunId, run2.RunId);
    }

    [Fact]
    public async Task SameUser_AfterComplete_CanStartNew()
    {
        var engine = Engine("user-a");

        var (run1, _) = await engine.StartRunAsync("checkout", _testEntries);
        await engine.FinalizeRunAsync(run1.RunId, force: true);

        var (run2, _) = await engine.StartRunAsync("checkout", _testEntries);
        Assert.NotEqual(run1.RunId, run2.RunId);
    }

    private sealed class MockUserIdentityResolver : IUserIdentityResolver
    {
        private readonly string _user;
        public MockUserIdentityResolver(string user) => _user = user;
        public string GetCurrentUser() => _user;
    }
}
