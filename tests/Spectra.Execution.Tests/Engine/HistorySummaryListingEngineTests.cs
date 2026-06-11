using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Engine;

/// <summary>
/// Spec 070 — ported from the MCP <c>GetRunHistoryTests</c> (GAP), <c>GetExecutionSummaryTests</c>, and
/// <c>ListActiveRunsToolTests</c> (GAP). These map to repository/engine queries: run-history =
/// <c>RunRepository.GetAllAsync(suite, user, limit, status)</c>; summary = <c>GetByIdAsync</c> +
/// <c>GetStatusCountsAsync</c>; list-active = <c>GetActiveRunsAsync</c> + per-run counts. The tool JSON
/// envelopes were transport and are retired. (list-suites is a CLI-layer suite-loader delegate, covered by
/// the <c>spectra run list-suites</c> path, not the engine.)
/// </summary>
public class HistorySummaryListingEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly McpConfig _config;

    public HistorySummaryListingEngineTests()
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
    public async Task History_ReturnsRuns_AndFiltersByStatus()
    {
        var engine = Engine();
        var (first, _) = await engine.StartRunAsync("checkout", Entries());
        await engine.FinalizeRunAsync(first.RunId, force: true); // → Completed
        await engine.StartRunAsync("checkout", Entries());        // → Running

        var all = await _runRepo.GetAllAsync(limit: 50);
        Assert.Equal(2, all.Count);

        var completed = await _runRepo.GetAllAsync(status: RunStatus.Completed);
        Assert.Single(completed);
        Assert.Equal(first.RunId, completed[0].RunId);
    }

    [Fact]
    public async Task Summary_ExposesRunAndPerStatusCounts()
    {
        var engine = Engine();
        var (run, queue) = await engine.StartRunAsync("checkout", Entries());
        var firstHandle = queue.GetNext()!.TestHandle;
        await engine.StartTestAsync(run.RunId, firstHandle);
        await engine.AdvanceTestAsync(run.RunId, firstHandle, TestStatus.Passed, null);

        var loaded = await _runRepo.GetByIdAsync(run.RunId);
        Assert.NotNull(loaded);

        var counts = await _resultRepo.GetStatusCountsAsync(run.RunId);
        Assert.Equal(1, counts.GetValueOrDefault(TestStatus.Passed));
        Assert.Equal(1, counts.GetValueOrDefault(TestStatus.Pending));
    }

    [Fact]
    public async Task ListActive_ReturnsActiveRunsWithResolvableCounts()
    {
        var engine = Engine();
        await engine.StartRunAsync("checkout", Entries());
        await engine.StartRunAsync("auth", Entries());

        var active = await _runRepo.GetActiveRunsAsync();
        Assert.Equal(2, active.Count);

        foreach (var run in active)
        {
            var counts = await _resultRepo.GetStatusCountsAsync(run.RunId);
            Assert.Equal(2, counts.Values.Sum()); // both tests tracked per run
        }
    }

    private sealed class MockUserIdentityResolver : IUserIdentityResolver
    {
        private readonly string _user;
        public MockUserIdentityResolver(string user) => _user = user;
        public string GetCurrentUser() => _user;
    }
}
