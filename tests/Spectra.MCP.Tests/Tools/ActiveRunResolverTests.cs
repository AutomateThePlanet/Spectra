using System.Text.Json;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools;

namespace Spectra.MCP.Tests.Tools;

public class ActiveRunResolverTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public ActiveRunResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // --- run_id resolution ---

    [Fact]
    public async Task ResolveRunId_ExplicitRunId_ReturnsSameId()
    {
        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync("explicit-id", _runRepo);

        Assert.Equal("explicit-id", runId);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveRunId_NoActiveRuns_ReturnsNoActiveRunsError()
    {
        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);

        Assert.Null(runId);
        Assert.NotNull(error);

        var response = JsonDocument.Parse(error).RootElement;
        Assert.Equal("NO_ACTIVE_RUNS", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task ResolveRunId_SingleActiveRun_ReturnsThatRunId()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));

        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);

        Assert.Equal("run-1", runId);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveRunId_SingleActiveRun_IgnoresTerminalRuns()
    {
        await _runRepo.CreateAsync(CreateRun("run-completed", "checkout", RunStatus.Completed));
        await _runRepo.CreateAsync(CreateRun("run-cancelled", "auth", RunStatus.Cancelled));
        await _runRepo.CreateAsync(CreateRun("run-active", "search", RunStatus.Running));

        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);

        Assert.Equal("run-active", runId);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveRunId_MultipleActiveRuns_ReturnsMultipleActiveRunsError()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _runRepo.CreateAsync(CreateRun("run-2", "auth", RunStatus.Paused));

        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);

        Assert.Null(runId);
        Assert.NotNull(error);

        var response = JsonDocument.Parse(error).RootElement;
        Assert.Equal("MULTIPLE_ACTIVE_RUNS", response.GetProperty("error").GetProperty("code").GetString());
        var message = response.GetProperty("error").GetProperty("message").GetString()!;
        Assert.Contains("run-1", message);
        Assert.Contains("run-2", message);
        Assert.Contains("checkout", message);
        Assert.Contains("auth", message);
    }

    [Fact]
    public async Task ResolveRunId_EmptyString_TreatedAsOmitted()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));

        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync("", _runRepo);

        Assert.Equal("run-1", runId);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveRunId_CreatedStatusRun_IsConsideredActive()
    {
        await _runRepo.CreateAsync(CreateRun("run-created", "checkout", RunStatus.Created));

        var (runId, error) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);

        Assert.Equal("run-created", runId);
        Assert.Null(error);
    }

    // --- test_handle resolution ---

    [Fact]
    public async Task ResolveTestHandle_ExplicitHandle_ReturnsSameHandle()
    {
        var (handle, error) = await ActiveRunResolver.ResolveTestHandleAsync("explicit-handle", "run-1", _resultRepo);

        Assert.Equal("explicit-handle", handle);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveTestHandle_NoInProgressTests_ReturnsError()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _resultRepo.CreateAsync(CreateResult("run-1", "TC-001", TestStatus.Pending));

        var (handle, error) = await ActiveRunResolver.ResolveTestHandleAsync(null, "run-1", _resultRepo);

        Assert.Null(handle);
        Assert.NotNull(error);

        var response = JsonDocument.Parse(error).RootElement;
        Assert.Equal("NO_TEST_IN_PROGRESS", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("get_execution_status", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task ResolveTestHandle_SingleInProgressTest_ReturnsThatHandle()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _resultRepo.CreateAsync(CreateResult("run-1", "TC-001", TestStatus.InProgress, "handle-tc001"));
        await _resultRepo.CreateAsync(CreateResult("run-1", "TC-002", TestStatus.Pending));

        var (handle, error) = await ActiveRunResolver.ResolveTestHandleAsync(null, "run-1", _resultRepo);

        Assert.Equal("handle-tc001", handle);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResolveTestHandle_MultipleInProgressTests_ReturnsError()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _resultRepo.CreateAsync(CreateResult("run-1", "TC-001", TestStatus.InProgress, "handle-tc001"));
        await _resultRepo.CreateAsync(CreateResult("run-1", "TC-002", TestStatus.InProgress, "handle-tc002"));

        var (handle, error) = await ActiveRunResolver.ResolveTestHandleAsync(null, "run-1", _resultRepo);

        Assert.Null(handle);
        Assert.NotNull(error);

        var response = JsonDocument.Parse(error).RootElement;
        Assert.Equal("MULTIPLE_TESTS_IN_PROGRESS", response.GetProperty("error").GetProperty("code").GetString());
        var message = response.GetProperty("error").GetProperty("message").GetString()!;
        Assert.Contains("handle-tc001", message);
        Assert.Contains("handle-tc002", message);
    }

    [Fact]
    public async Task ResolveTestHandle_EmptyString_TreatedAsOmitted()
    {
        await _runRepo.CreateAsync(CreateRun("run-1", "checkout", RunStatus.Running));
        await _resultRepo.CreateAsync(CreateResult("run-1", "TC-001", TestStatus.InProgress, "handle-tc001"));

        var (handle, error) = await ActiveRunResolver.ResolveTestHandleAsync("", "run-1", _resultRepo);

        Assert.Equal("handle-tc001", handle);
        Assert.Null(error);
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

    private static TestResult CreateResult(string runId, string testId, TestStatus status, string? handle = null) => new()
    {
        RunId = runId,
        TestId = testId,
        TestHandle = handle ?? $"handle-{testId}",
        Status = status,
        Attempt = 1,
        StartedAt = status == TestStatus.InProgress ? DateTime.UtcNow : null
    };
}
