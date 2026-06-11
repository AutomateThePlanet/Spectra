using Spectra.Core.Models.Execution;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Storage;

public class RunRepositoryTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _repo;

    public RunRepositoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _repo = new RunRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task GetActiveRunAsync_NoActiveRun_ReturnsNull()
    {
        var result = await _repo.GetActiveRunAsync("checkout", "user-a");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveRunAsync_CompletedRun_ReturnsNull()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Completed, "user-a"));

        var result = await _repo.GetActiveRunAsync("checkout", "user-a");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveRunAsync_RunningRun_ReturnsRun()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running, "user-a"));

        var result = await _repo.GetActiveRunAsync("checkout", "user-a");

        Assert.NotNull(result);
        Assert.Equal("run-1", result.RunId);
    }

    [Fact]
    public async Task GetActiveRunAsync_PausedRun_ReturnsRun()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Paused, "user-a"));

        var result = await _repo.GetActiveRunAsync("checkout", "user-a");

        Assert.NotNull(result);
        Assert.Equal("run-1", result.RunId);
    }

    [Fact]
    public async Task GetActiveRunAsync_DifferentUser_ReturnsNull()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running, "user-a"));

        var result = await _repo.GetActiveRunAsync("checkout", "user-b");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveRunAsync_DifferentSuite_ReturnsNull()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running, "user-a"));

        var result = await _repo.GetActiveRunAsync("auth", "user-a");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveRunAsync_MultipleUsers_ReturnsCorrectRun()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running, "user-a"));
        await _repo.CreateAsync(CreateRun("run-2", "checkout", RunStatus.Running, "user-b"));

        var resultA = await _repo.GetActiveRunAsync("checkout", "user-a");
        var resultB = await _repo.GetActiveRunAsync("checkout", "user-b");

        Assert.NotNull(resultA);
        Assert.Equal("run-1", resultA.RunId);
        Assert.NotNull(resultB);
        Assert.Equal("run-2", resultB.RunId);
    }

    [Fact]
    public async Task GetAbandonedRunsAsync_NoPausedRuns_ReturnsEmpty()
    {
        await _repo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running, "user-a"));

        var result = await _repo.GetAbandonedRunsAsync(TimeSpan.FromHours(72));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAbandonedRunsAsync_RecentlyPaused_ReturnsEmpty()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Paused, "user-a", DateTime.UtcNow.AddHours(-1));
        await _repo.CreateAsync(run);

        var result = await _repo.GetAbandonedRunsAsync(TimeSpan.FromHours(72));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAbandonedRunsAsync_OldPaused_ReturnsRun()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Paused, "user-a", DateTime.UtcNow.AddHours(-80));
        await _repo.CreateAsync(run);

        var result = await _repo.GetAbandonedRunsAsync(TimeSpan.FromHours(72));

        Assert.Single(result);
        Assert.Equal("run-1", result[0].RunId);
    }

    private static Run CreateRun(string runId, string suite, RunStatus status, string user, DateTime? updatedAt = null) => new()
    {
        RunId = runId,
        Suite = suite,
        Status = status,
        StartedAt = DateTime.UtcNow.AddMinutes(-30),
        StartedBy = user,
        UpdatedAt = updatedAt ?? DateTime.UtcNow
    };
}
