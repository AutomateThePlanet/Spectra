using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Execution;

/// <summary>
/// US1 (spec 064): dependency blocking survives reconstruction. A queue rebuilt from the durable
/// snapshot in a fresh process must restore <c>DependsOn</c> and block dependents identically to the
/// original in-memory queue — verified both at the queue level and through a second engine that
/// reconstructs from the same DB.
/// </summary>
public class ReconstructionBlockingParityTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly McpConfig _config;

    // Chain: TC-002 -> TC-001, TC-003 -> TC-002 (transitive), TC-004 independent.
    private readonly List<TestIndexEntry> _entries =
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Login", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Checkout", Priority = "high", DependsOn = "TC-001" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "Payment", Priority = "medium", DependsOn = "TC-002" },
        new() { Id = "TC-004", File = "tc-004.md", Title = "Logout", Priority = "low" }
    ];

    public ReconstructionBlockingParityTests()
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
    public async Task ReconstructedQueue_RestoresDependsOn()
    {
        var origin = NewEngine();
        var (run, original) = await origin.StartRunAsync("checkout", _entries);

        // Fresh engine (cold _queues) reconstructs from the DB.
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(reconstructed);
        foreach (var orig in original.Tests)
        {
            var rebuilt = reconstructed!.GetById(orig.TestId);
            Assert.NotNull(rebuilt);
            Assert.Equal(orig.DependsOn, rebuilt!.DependsOn);
        }
    }

    [Fact]
    public async Task PropagateBlocks_OnReconstructedQueue_MatchesOriginal()
    {
        var origin = NewEngine();
        var (run, original) = await origin.StartRunAsync("checkout", _entries);
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);
        Assert.NotNull(reconstructed);

        var resolver = new DependencyResolver();
        var originalBlocked = resolver.PropagateBlocks(original, "TC-001").OrderBy(x => x).ToList();
        var reconstructedBlocked = resolver.PropagateBlocks(reconstructed!, "TC-001").OrderBy(x => x).ToList();

        // Original blocks the transitive chain TC-002, TC-003; reconstructed must match exactly.
        Assert.Equal(new[] { "TC-002", "TC-003" }, originalBlocked);
        Assert.Equal(originalBlocked, reconstructedBlocked);
    }

    [Fact]
    public async Task FreshEngine_Advance_CascadesBlocksAcrossProcessBoundary()
    {
        // Original process starts the run and holds the rich queue.
        var origin = NewEngine();
        var (run, original) = await origin.StartRunAsync("checkout", _entries);
        var tc001 = original.Tests.First(t => t.TestId == "TC-001").TestHandle;

        // A different (short-lived) engine over the same DB drives the failure — no cached queue.
        var fresh = NewEngine();
        await fresh.StartTestAsync(run.RunId, tc001);
        var (_, blocked, next) = await fresh.AdvanceTestAsync(run.RunId, tc001, TestStatus.Failed, "Login broken");

        // Dependency cascade fires from the reconstructed queue (the old path silently no-op'd here).
        Assert.Contains("TC-002", blocked);
        Assert.Contains("TC-003", blocked);
        Assert.DoesNotContain("TC-004", blocked);
        // Next actionable test is the independent TC-004, not blocked work.
        Assert.Equal("TC-004", next?.TestId);
    }

    [Fact]
    public async Task FreshEngine_AdvanceBlocks_ArePersisted()
    {
        var origin = NewEngine();
        var (run, original) = await origin.StartRunAsync("checkout", _entries);
        var tc001 = original.Tests.First(t => t.TestId == "TC-001").TestHandle;

        var fresh = NewEngine();
        await fresh.StartTestAsync(run.RunId, tc001);
        await fresh.AdvanceTestAsync(run.RunId, tc001, TestStatus.Failed, "Login broken");

        // The blocked status is durable — visible to yet another fresh engine.
        var counts = await NewEngine().GetStatusCountsAsync(run.RunId);
        Assert.Equal(2, counts.GetValueOrDefault(TestStatus.Blocked));
    }
}
