using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Engine;

/// <summary>
/// Spec 070 — ported from the MCP <c>SkipTestCaseTests</c> + <c>AddTestNoteTests</c> to exercise
/// <see cref="ExecutionEngine"/> directly (the transport tool wrapper is removed). Covers skip → SKIPPED +
/// reason persisted + dependent blocking + invalid/illegal-state guards, and note append + multi-note
/// accumulation. Tool-layer param-presence/error-code mapping (INVALID_PARAMS / INVALID_HANDLE strings)
/// was transport-only and is retired with the adapter.
/// </summary>
public class AdvanceSkipNoteEngineTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ResultRepository _resultRepo;
    private readonly ExecutionEngine _engine;
    private readonly List<TestIndexEntry> _testEntries;

    public AdvanceSkipNoteEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _engine = new ExecutionEngine(runRepo, _resultRepo, new QueueSnapshotRepository(_db),
            new UserIdentityResolver(), new McpConfig { BasePath = _testDir });

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", DependsOn = "TC-001" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low", DependsOn = "TC-002" }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Skip_RecordsSkippedWithReason_AndBlocksDependents()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var first = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, first.TestHandle);

        var (skipped, blocked, _) = await _engine.SkipTestAsync(run.RunId, first.TestHandle, "Environment not available");

        Assert.Equal("TC-001", skipped.TestId);
        Assert.Equal(TestStatus.Skipped, skipped.Status);
        Assert.Contains("TC-002", blocked);

        // Reason is persisted on the stored row (the returned value predates the notes write).
        var persisted = await _resultRepo.GetByHandleAsync(first.TestHandle);
        Assert.Equal("Environment not available", persisted!.Notes);
        Assert.Equal(TestStatus.Skipped, persisted.Status);
    }

    [Fact]
    public async Task Skip_InvalidHandle_Throws()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _engine.SkipTestAsync("any", "invalid", "reason"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Skip_TestNotInProgress_Throws()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var first = queue.Tests.First();

        // Not started → the state machine rejects skip from a non-in-progress state.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _engine.SkipTestAsync(run.RunId, first.TestHandle, "reason"));
    }

    [Fact]
    public async Task AddNote_PersistsNote_OnInProgressTest()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var first = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, first.TestHandle);

        await _engine.AddNoteAsync(first.TestHandle, "UI shows spinner");

        var updated = await _resultRepo.GetByHandleAsync(first.TestHandle);
        Assert.NotNull(updated);
        Assert.Equal("UI shows spinner", updated!.Notes);
    }

    [Fact]
    public async Task AddNote_MultipleNotes_AccumulateNewlineSeparated()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var first = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, first.TestHandle);

        await _engine.AddNoteAsync(first.TestHandle, "Note 1");
        await _engine.AddNoteAsync(first.TestHandle, "Note 2");

        var updated = await _resultRepo.GetByHandleAsync(first.TestHandle);
        var noteCount = updated!.Notes!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(2, noteCount);
    }
}
