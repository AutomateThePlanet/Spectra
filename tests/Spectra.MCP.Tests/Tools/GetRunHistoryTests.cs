using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.Reporting;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class GetRunHistoryTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly GetRunHistoryTool _tool;

    public GetRunHistoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _tool = new GetRunHistoryTool(_runRepo);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_NoRuns_ReturnsEmptyList()
    {
        var result = await _tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(0, data.GetProperty("runs").GetArrayLength());
    }

    [Fact]
    public async Task Execute_WithRuns_ReturnsRunList()
    {
        // Create some runs
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-2", "auth", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-3", "payment", RunStatus.Running));

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var runs = response.GetProperty("data").GetProperty("runs");
        Assert.Equal(3, runs.GetArrayLength());
    }

    [Fact]
    public async Task Execute_WithLimit_ReturnsLimitedResults()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-2", "auth", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-3", "payment", RunStatus.Completed));

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"limit": 2}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var runs = response.GetProperty("data").GetProperty("runs");
        Assert.Equal(2, runs.GetArrayLength());
    }

    [Fact]
    public async Task Execute_WithUserFilter_ReturnsUserRuns()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Completed, "user-a"));
        await _runRepo.CreateAsync(CreateRun("run-2", "auth", RunStatus.Completed, "user-b"));
        await _runRepo.CreateAsync(CreateRun("run-3", "payment", RunStatus.Completed, "user-a"));

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"user": "user-a"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var runs = response.GetProperty("data").GetProperty("runs");
        Assert.Equal(2, runs.GetArrayLength());
    }

    [Fact]
    public async Task Execute_WithSuiteFilter_ReturnsSuiteRuns()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-2", "auth", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-3", "checkout", RunStatus.Completed));

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var runs = response.GetProperty("data").GetProperty("runs");
        Assert.Equal(2, runs.GetArrayLength());
    }

    [Fact]
    public async Task Execute_ReturnsRunDetails()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Completed);
        await _runRepo.CreateAsync(run);

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var runData = response.GetProperty("data").GetProperty("runs")[0];
        Assert.Equal("run-1", runData.GetProperty("run_id").GetString());
        Assert.Equal("checkout", runData.GetProperty("suite").GetString());
        Assert.Equal("Completed", runData.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_OrdersByStartedAtDescending()
    {
        var now = DateTime.UtcNow;
        await _runRepo.CreateAsync(CreateRun("run-1", "a", RunStatus.Completed, startedAt: now.AddHours(-3)));
        await _runRepo.CreateAsync(CreateRun("run-2", "b", RunStatus.Completed, startedAt: now.AddHours(-1)));
        await _runRepo.CreateAsync(CreateRun("run-3", "c", RunStatus.Completed, startedAt: now.AddHours(-2)));

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var runs = response.GetProperty("data").GetProperty("runs");
        Assert.Equal("run-2", runs[0].GetProperty("run_id").GetString()); // Most recent first
        Assert.Equal("run-3", runs[1].GetProperty("run_id").GetString());
        Assert.Equal("run-1", runs[2].GetProperty("run_id").GetString());
    }

    private static Run CreateRun(
        string runId,
        string suite,
        RunStatus status,
        string user = "test-user",
        DateTime? startedAt = null) => new()
    {
        RunId = runId,
        Suite = suite,
        Status = status,
        StartedAt = startedAt ?? DateTime.UtcNow,
        StartedBy = user,
        UpdatedAt = DateTime.UtcNow,
        CompletedAt = status == RunStatus.Completed ? DateTime.UtcNow : null
    };
}
