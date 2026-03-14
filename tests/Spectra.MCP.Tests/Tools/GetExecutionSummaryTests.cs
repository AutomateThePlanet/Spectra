using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.Reporting;

namespace Spectra.MCP.Tests.Tools;

public class GetExecutionSummaryTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly GetExecutionSummaryTool _tool;

    public GetExecutionSummaryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _tool = new GetExecutionSummaryTool(_runRepo, _resultRepo);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_InvalidRunId_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "nonexistent"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("RUN_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ValidRun_ReturnsSummary()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Completed);
        await _runRepo.CreateAsync(run);
        await _resultRepo.CreateManyAsync(new[]
        {
            CreateResult("run-1", "TC-001", TestStatus.Passed),
            CreateResult("run-1", "TC-002", TestStatus.Failed),
            CreateResult("run-1", "TC-003", TestStatus.Passed)
        });

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "run-1"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal("run-1", data.GetProperty("run_id").GetString());
        Assert.Equal("checkout", data.GetProperty("suite").GetString());
    }

    [Fact]
    public async Task Execute_ReturnsStatusCounts()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Completed);
        await _runRepo.CreateAsync(run);
        await _resultRepo.CreateManyAsync(new[]
        {
            CreateResult("run-1", "TC-001", TestStatus.Passed),
            CreateResult("run-1", "TC-002", TestStatus.Passed),
            CreateResult("run-1", "TC-003", TestStatus.Failed),
            CreateResult("run-1", "TC-004", TestStatus.Skipped),
            CreateResult("run-1", "TC-005", TestStatus.Blocked)
        });

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "run-1"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var summary = response.GetProperty("data").GetProperty("summary");
        Assert.Equal(5, summary.GetProperty("total").GetInt32());
        Assert.Equal(2, summary.GetProperty("passed").GetInt32());
        Assert.Equal(1, summary.GetProperty("failed").GetInt32());
        Assert.Equal(1, summary.GetProperty("skipped").GetInt32());
        Assert.Equal(1, summary.GetProperty("blocked").GetInt32());
    }

    [Fact]
    public async Task Execute_ReturnsPassRate()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Completed);
        await _runRepo.CreateAsync(run);
        await _resultRepo.CreateManyAsync(new[]
        {
            CreateResult("run-1", "TC-001", TestStatus.Passed),
            CreateResult("run-1", "TC-002", TestStatus.Passed),
            CreateResult("run-1", "TC-003", TestStatus.Failed),
            CreateResult("run-1", "TC-004", TestStatus.Passed),
            CreateResult("run-1", "TC-005", TestStatus.Failed)
        });

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "run-1"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var passRate = response.GetProperty("data").GetProperty("summary").GetProperty("pass_rate").GetDouble();
        Assert.Equal(60.0, passRate);
    }

    [Fact]
    public async Task Execute_RunningRun_ShowsProgress()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Running);
        await _runRepo.CreateAsync(run);
        await _resultRepo.CreateManyAsync(new[]
        {
            CreateResult("run-1", "TC-001", TestStatus.Passed),
            CreateResult("run-1", "TC-002", TestStatus.Pending),
            CreateResult("run-1", "TC-003", TestStatus.Pending)
        });

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "run-1"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Running", response.GetProperty("run_status").GetString());
        Assert.Equal("1/3", response.GetProperty("progress").GetString());
    }

    [Fact]
    public async Task Execute_ReturnsNextExpectedAction()
    {
        var run = CreateRun("run-1", "checkout", RunStatus.Running);
        await _runRepo.CreateAsync(run);

        var result = await _tool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "run-1"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("get_test_case_details", response.GetProperty("next_expected_action").GetString());
    }

    private static Run CreateRun(string runId, string suite, RunStatus status) => new()
    {
        RunId = runId,
        Suite = suite,
        Status = status,
        StartedAt = DateTime.UtcNow.AddMinutes(-30),
        StartedBy = "test-user",
        UpdatedAt = DateTime.UtcNow,
        CompletedAt = status == RunStatus.Completed ? DateTime.UtcNow : null
    };

    private static TestResult CreateResult(string runId, string testId, TestStatus status) => new()
    {
        RunId = runId,
        TestId = testId,
        TestHandle = $"handle-{testId}",
        Status = status,
        Attempt = 1
    };
}
