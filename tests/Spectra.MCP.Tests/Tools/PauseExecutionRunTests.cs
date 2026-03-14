using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class PauseExecutionRunTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly PauseExecutionRunTool _tool;
    private readonly TestIndexEntry[] _testEntries;

    public PauseExecutionRunTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir };

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);
        _tool = new PauseExecutionRunTool(_engine);

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = ["regression"] }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_RunningRun_ReturnsPausedStatus()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", _testEntries);
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{run.RunId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Paused", response.GetProperty("run_status").GetString());
        Assert.Equal("resume_execution_run", response.GetProperty("next_expected_action").GetString());
        Assert.Equal(run.RunId, response.GetProperty("data").GetProperty("run_id").GetString());
        Assert.True(response.GetProperty("data").TryGetProperty("paused_at", out _));
    }

    [Fact]
    public async Task Execute_InvalidRunId_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{"run_id": "nonexistent"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("RUN_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MissingRunId_ReturnsError()
    {
        var parameters = JsonDocument.Parse("{}").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_PARAMS", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_AlreadyPausedRun_ReturnsError()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", _testEntries);
        await _engine.PauseRunAsync(run.RunId);
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{run.RunId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_TRANSITION", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Paused", response.GetProperty("current_run_status").GetString());
    }

    [Fact]
    public async Task Execute_CompletedRun_ReturnsError()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);

        // Complete all tests
        var handles = queue.Tests.Select(t => t.TestHandle).ToList();
        foreach (var handle in handles)
        {
            await _engine.StartTestAsync(run.RunId, handle);
            await _engine.AdvanceTestAsync(run.RunId, handle, TestStatus.Passed);
        }
        await _engine.FinalizeRunAsync(run.RunId);

        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{run.RunId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_TRANSITION", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_PreservesProgress()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);

        // Complete first test
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);
        await _engine.AdvanceTestAsync(run.RunId, firstTest.TestHandle, TestStatus.Passed);

        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{run.RunId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("1/2", response.GetProperty("progress").GetString());
    }
}
