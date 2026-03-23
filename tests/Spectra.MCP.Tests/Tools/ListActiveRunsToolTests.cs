using System.Text.Json;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class ListActiveRunsToolTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly ListActiveRunsTool _tool;

    public ListActiveRunsToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _tool = new ListActiveRunsTool(_runRepo, _resultRepo);
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

        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.Equal("No active runs found.", data.GetProperty("message").GetString());
        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_SingleActiveRun_ReturnsItWithProgress()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _resultRepo.CreateManyAsync(new[]
        {
            CreateResult("run-1", "TC-001", TestStatus.Passed),
            CreateResult("run-1", "TC-002", TestStatus.Failed),
            CreateResult("run-1", "TC-003", TestStatus.Pending)
        });

        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;
        var data = response.GetProperty("data");
        var runs = data.GetProperty("runs");

        Assert.Equal(1, data.GetProperty("count").GetInt32());
        Assert.Equal(1, runs.GetArrayLength());

        var run = runs[0];
        Assert.Equal("run-1", run.GetProperty("run_id").GetString());
        Assert.Equal("checkout", run.GetProperty("suite").GetString());
        Assert.Equal("Running", run.GetProperty("status").GetString());
        Assert.Contains("2/3 completed", run.GetProperty("progress").GetString());
    }

    [Fact]
    public async Task Execute_MixedRuns_ReturnsOnlyActive()
    {
        await _runRepo.CreateAsync(CreateRun("run-running", "checkout", RunStatus.Running));
        await _runRepo.CreateAsync(CreateRun("run-paused", "auth", RunStatus.Paused));
        await _runRepo.CreateAsync(CreateRun("run-completed", "search", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-cancelled", "login", RunStatus.Cancelled));

        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;
        var data = response.GetProperty("data");
        var runs = data.GetProperty("runs");

        Assert.Equal(2, data.GetProperty("count").GetInt32());
        var runIds = Enumerable.Range(0, runs.GetArrayLength())
            .Select(i => runs[i].GetProperty("run_id").GetString())
            .ToList();
        Assert.Contains("run-running", runIds);
        Assert.Contains("run-paused", runIds);
        Assert.DoesNotContain("run-completed", runIds);
        Assert.DoesNotContain("run-cancelled", runIds);
    }

    [Fact]
    public async Task Execute_CreatedRunIsIncluded()
    {
        await _runRepo.CreateAsync(CreateRun("run-created", "checkout", RunStatus.Created));

        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;
        var data = response.GetProperty("data");

        Assert.Equal(1, data.GetProperty("count").GetInt32());
        Assert.Equal("Created", data.GetProperty("runs")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_ReturnsNextExpectedAction()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));

        var result = await _tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("get_execution_status", response.GetProperty("next_expected_action").GetString());
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

    private static TestResult CreateResult(string runId, string testId, TestStatus status) => new()
    {
        RunId = runId,
        TestId = testId,
        TestHandle = $"handle-{testId}",
        Status = status,
        Attempt = 1
    };
}
