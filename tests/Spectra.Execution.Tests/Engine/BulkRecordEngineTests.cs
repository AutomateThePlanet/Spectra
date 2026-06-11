using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Engine;

/// <summary>
/// Spec 070 — ported from the MCP <c>BulkRecordResultsTests</c> to exercise
/// <see cref="ExecutionEngine.BulkRecordResultsAsync"/> directly (GAP: no prior engine/CLI coverage of
/// the bulk path). Covers recording a batch of handles to a terminal status (count + applied status +
/// reason persisted) and block propagation through the bulk path. The "remaining" / "by-test-ids"
/// resolution that the retired tool layer performed now lives in the CLI <c>run bulk-record</c> handler
/// and is covered there (lifecycle smoke).
/// </summary>
public class BulkRecordEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ResultRepository _resultRepo;
    private readonly ExecutionEngine _engine;

    public BulkRecordEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _engine = new ExecutionEngine(runRepo, _resultRepo, new QueueSnapshotRepository(_db),
            new UserIdentityResolver(), new McpConfig { BasePath = _testDir });
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static List<TestIndexEntry> Flat() =>
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "One", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Two", Priority = "medium" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "Three", Priority = "low" },
        new() { Id = "TC-004", File = "tc-004.md", Title = "Four", Priority = "low" },
        new() { Id = "TC-005", File = "tc-005.md", Title = "Five", Priority = "low" }
    ];

    private async Task<List<string>> PendingHandles(string runId)
    {
        var results = await _engine.GetResultsAsync(runId);
        return results.Where(r => r.Status == TestStatus.Pending).Select(r => r.TestHandle).ToList();
    }

    [Theory]
    [InlineData("PASSED")]
    [InlineData("SKIPPED")]
    [InlineData("FAILED")]
    public async Task BulkRecord_AppliesStatusToAllHandles(string statusName)
    {
        var status = Enum.Parse<TestStatus>(statusName, ignoreCase: true);
        var (run, _) = await _engine.StartRunAsync("checkout", Flat());
        var handles = await PendingHandles(run.RunId);

        var result = await _engine.BulkRecordResultsAsync(run.RunId, handles, status, "bulk reason");

        Assert.Equal(5, result.ProcessedCount);
        var rows = await _engine.GetResultsAsync(run.RunId);
        Assert.All(rows, r => Assert.Equal(status, r.Status));
    }

    [Fact]
    public async Task BulkRecord_PersistsReason()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", Flat());
        var handles = await PendingHandles(run.RunId);

        await _engine.BulkRecordResultsAsync(run.RunId, handles, TestStatus.Skipped, "Environment not available");

        var rows = await _engine.GetResultsAsync(run.RunId);
        Assert.All(rows, r => Assert.Equal("Environment not available", r.Notes));
    }

    [Fact]
    public async Task BulkRecord_FailingABlocker_PropagatesBlocksThroughBulkPath()
    {
        var entries = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Parent", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Child", Priority = "high", DependsOn = "TC-001" }
        };
        var (run, queue) = await _engine.StartRunAsync("checkout", entries);
        var parentHandle = queue.GetById("TC-001")!.TestHandle;

        var result = await _engine.BulkRecordResultsAsync(run.RunId, [parentHandle], TestStatus.Failed, "parent broke");

        Assert.Contains("TC-002", result.BlockedTests);
        var child = (await _engine.GetResultsAsync(run.RunId)).First(r => r.TestId == "TC-002");
        Assert.Equal(TestStatus.Blocked, child.Status);
    }
}
