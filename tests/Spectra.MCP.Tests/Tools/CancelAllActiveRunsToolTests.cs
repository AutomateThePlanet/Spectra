using System.Text.Json;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class CancelAllActiveRunsToolTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly CancelAllActiveRunsTool _tool;

    public CancelAllActiveRunsToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir, ReportsPath = Path.Combine(_testDir, "reports") };
        var engine = new ExecutionEngine(_runRepo, _resultRepo, identity, config);

        _tool = new CancelAllActiveRunsTool(engine, _runRepo);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_NoActiveRuns_ReturnsEmptyWithMessage()
    {
        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;
        var data = response.GetProperty("data");

        Assert.Equal(0, data.GetProperty("cancelled_count").GetInt32());
        Assert.Equal("No active runs to cancel.", data.GetProperty("message").GetString());
        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_MultipleActiveRuns_CancelsAll()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _runRepo.CreateAsync(CreateRun("run-2", "auth", RunStatus.Paused));

        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;
        var data = response.GetProperty("data");

        Assert.Equal(2, data.GetProperty("cancelled_count").GetInt32());
        var cancelled = data.GetProperty("cancelled");
        Assert.Equal(2, cancelled.GetArrayLength());

        // Verify previous statuses are recorded
        var statuses = Enumerable.Range(0, cancelled.GetArrayLength())
            .Select(i => cancelled[i].GetProperty("previous_status").GetString())
            .OrderBy(s => s)
            .ToList();
        Assert.Contains("Paused", statuses);
        Assert.Contains("Running", statuses);

        // Verify runs are now cancelled in DB
        var run1 = await _runRepo.GetByIdAsync("run-1");
        Assert.Equal(RunStatus.Cancelled, run1!.Status);
    }

    [Fact]
    public async Task Execute_MixedRuns_OnlyCancelsActive()
    {
        await _runRepo.CreateAsync(CreateRun("run-active", "checkout", RunStatus.Running));
        await _runRepo.CreateAsync(CreateRun("run-completed", "auth", RunStatus.Completed));

        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;
        var data = response.GetProperty("data");

        Assert.Equal(1, data.GetProperty("cancelled_count").GetInt32());
        Assert.Equal("run-active", data.GetProperty("cancelled")[0].GetProperty("run_id").GetString());

        // Completed run should not be affected
        var completedRun = await _runRepo.GetByIdAsync("run-completed");
        Assert.Equal(RunStatus.Completed, completedRun!.Status);
    }

    [Fact]
    public async Task Execute_WithReason_PassesReasonThrough()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));

        var parameters = JsonDocument.Parse("""{"reason": "cleanup"}""").RootElement;
        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(1, response.GetProperty("data").GetProperty("cancelled_count").GetInt32());
    }

    [Fact]
    public async Task Execute_ReturnsNextExpectedAction()
    {
        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    private static Run CreateRun(string runId, string suite, RunStatus status) => new()
    {
        RunId = runId,
        Suite = suite,
        Status = status,
        StartedAt = DateTime.UtcNow.AddMinutes(-30),
        StartedBy = "test-user",
        UpdatedAt = DateTime.UtcNow,
        CompletedAt = status is RunStatus.Completed or RunStatus.Cancelled ? DateTime.UtcNow : null
    };
}
