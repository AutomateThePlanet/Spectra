using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Execution;

/// <summary>
/// US3 (spec 064): reconstruction fails loud when it cannot faithfully rebuild the queue, and the
/// failure is distinct from the benign "run not found" (null) signal. Each test seeds a deliberately
/// corrupt DB state via the repositories and asserts <see cref="QueueReconstructionException"/>.
/// </summary>
public class ReconstructionFailLoudTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly McpConfig _config;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly QueueSnapshotRepository _snapshotRepo;

    public ReconstructionFailLoudTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _db = new ExecutionDb(_testDir);
        _config = new McpConfig { BasePath = _testDir };
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _snapshotRepo = new QueueSnapshotRepository(_db);
    }

    private ExecutionEngine NewEngine() => new(
        new RunRepository(_db), new ResultRepository(_db), new QueueSnapshotRepository(_db),
        new UserIdentityResolver(), _config);

    private async Task<string> SeedRunAsync()
    {
        var runId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await _runRepo.CreateAsync(new Run
        {
            RunId = runId,
            Suite = "suite",
            Status = RunStatus.Running,
            StartedAt = now,
            StartedBy = "tester",
            UpdatedAt = now
        });
        return runId;
    }

    private Task SeedResultAsync(string runId, string testId) =>
        _resultRepo.CreateAsync(new TestResult
        {
            RunId = runId,
            TestId = testId,
            TestHandle = $"{runId[..8]}-{testId}-x",
            Status = TestStatus.Pending,
            Attempt = 1
        });

    private QueueSnapshotEntry Snap(string runId, string testId, int order, string? dependsOn = null) => new()
    {
        RunId = runId,
        TestId = testId,
        Title = testId,
        Priority = "medium",
        DependsOn = dependsOn,
        OrderIndex = order
    };

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task ResultsButNoSnapshot_ThrowsReconstructionException()
    {
        var runId = await SeedRunAsync();
        await SeedResultAsync(runId, "TC-001"); // no snapshot at all

        var ex = await Assert.ThrowsAsync<QueueReconstructionException>(
            () => NewEngine().GetQueueAsync(runId));
        Assert.Equal(runId, ex.RunId);
    }

    [Fact]
    public async Task ResultWithoutSnapshotRow_ThrowsReconstructionException()
    {
        var runId = await SeedRunAsync();
        await _snapshotRepo.CreateManyAsync([Snap(runId, "TC-001", 0)]);
        await SeedResultAsync(runId, "TC-001");
        await SeedResultAsync(runId, "TC-002"); // recorded but absent from snapshot

        await Assert.ThrowsAsync<QueueReconstructionException>(
            () => NewEngine().GetQueueAsync(runId));
    }

    [Fact]
    public async Task SnapshotRowWithoutResult_ThrowsReconstructionException()
    {
        var runId = await SeedRunAsync();
        await _snapshotRepo.CreateManyAsync([Snap(runId, "TC-001", 0), Snap(runId, "TC-002", 1)]);
        await SeedResultAsync(runId, "TC-001"); // TC-002 in snapshot has no result

        await Assert.ThrowsAsync<QueueReconstructionException>(
            () => NewEngine().GetQueueAsync(runId));
    }

    [Fact]
    public async Task DanglingDependsOn_ThrowsReconstructionException()
    {
        var runId = await SeedRunAsync();
        await _snapshotRepo.CreateManyAsync([Snap(runId, "TC-002", 0, dependsOn: "TC-999")]);
        await SeedResultAsync(runId, "TC-002");

        await Assert.ThrowsAsync<QueueReconstructionException>(
            () => NewEngine().GetQueueAsync(runId));
    }

    [Fact]
    public async Task NoResultsAndNoSnapshot_ReturnsNull_BenignNotFound()
    {
        // A run id that genuinely has nothing recorded is a benign absence, not a corruption.
        var queue = await NewEngine().GetQueueAsync(Guid.NewGuid().ToString());
        Assert.Null(queue);
    }
}
