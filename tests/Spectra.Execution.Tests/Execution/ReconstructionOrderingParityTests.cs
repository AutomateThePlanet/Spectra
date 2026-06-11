using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Execution;

/// <summary>
/// US2 (spec 064): priority and ordering survive reconstruction. The fixture is deliberately
/// constructed so the correct priority-then-topological order differs from alphabetical-by-id,
/// guarding against the old lossy path that re-ordered <c>OrderBy(test_id)</c>.
/// </summary>
public class ReconstructionOrderingParityTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly McpConfig _config;

    // Correct order = [TC-002 (high), TC-003 (high, dep TC-002), TC-001 (low)].
    // Alphabetical order would be [TC-001, TC-002, TC-003] — intentionally different.
    private readonly List<TestIndexEntry> _entries =
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Low priority", Priority = "low" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "High first", Priority = "high" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "High dependent", Priority = "high", DependsOn = "TC-002" }
    ];

    public ReconstructionOrderingParityTests()
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
    public async Task ReconstructedQueue_PreservesOrder_NotAlphabetical()
    {
        var (run, original) = await NewEngine().StartRunAsync("suite", _entries);
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(reconstructed);
        var originalOrder = original.Tests.Select(t => t.TestId).ToList();
        var reconstructedOrder = reconstructed!.Tests.Select(t => t.TestId).ToList();

        Assert.Equal(new[] { "TC-002", "TC-003", "TC-001" }, originalOrder);
        Assert.Equal(originalOrder, reconstructedOrder);
        // Explicitly assert it did NOT fall back to alphabetical.
        Assert.NotEqual(new[] { "TC-001", "TC-002", "TC-003" }, reconstructedOrder);
    }

    [Fact]
    public async Task ReconstructedQueue_PreservesPriority()
    {
        var (run, original) = await NewEngine().StartRunAsync("suite", _entries);
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(reconstructed);
        foreach (var orig in original.Tests)
        {
            var rebuilt = reconstructed!.GetById(orig.TestId);
            Assert.NotNull(rebuilt);
            Assert.Equal(orig.Priority, rebuilt!.Priority);
        }
        // Sanity: the fixture really does carry non-default priorities.
        Assert.Equal(Priority.High, reconstructed!.GetById("TC-002")!.Priority);
        Assert.Equal(Priority.Low, reconstructed.GetById("TC-001")!.Priority);
    }

    [Fact]
    public async Task ReconstructedQueue_GetNext_MatchesOriginalSelection()
    {
        var (run, original) = await NewEngine().StartRunAsync("suite", _entries);
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(reconstructed);
        // First actionable test is the highest-priority root, not alphabetical TC-001.
        Assert.Equal("TC-002", original.GetNext()?.TestId);
        Assert.Equal(original.GetNext()?.TestId, reconstructed!.GetNext()?.TestId);
    }

    [Fact]
    public async Task ReconstructedQueue_PreservesTitle()
    {
        var (run, original) = await NewEngine().StartRunAsync("suite", _entries);
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(reconstructed);
        // Title is restored from the snapshot (the old path used the TestId as a stand-in).
        Assert.Equal("High first", reconstructed!.GetById("TC-002")!.Title);
        Assert.NotEqual("TC-002", reconstructed.GetById("TC-002")!.Title);
    }
}
