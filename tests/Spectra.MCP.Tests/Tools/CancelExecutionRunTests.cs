using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class CancelExecutionRunTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly CancelExecutionRunTool _cancelTool;

    public CancelExecutionRunTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir, ReportsPath = Path.Combine(_testDir, "reports") };

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);

        var testEntries = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium" }
        };

        _startTool = new StartExecutionRunTool(_engine, _ => testEntries);
        _cancelTool = new CancelExecutionRunTool(_engine, runRepo);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_InvalidRunId_ReturnsError()
    {
        var result = await _cancelTool.ExecuteAsync(JsonDocument.Parse("""{"run_id": "nonexistent"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("RUN_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_RunningRun_CancelsSuccessfully()
    {
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var runId = JsonDocument.Parse(startResult).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        var result = await _cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(runId, data.GetProperty("run_id").GetString());
        Assert.Equal("Cancelled", response.GetProperty("run_status").GetString());
    }

    [Fact]
    public async Task Execute_WithReason_IncludesReasonInResponse()
    {
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var runId = JsonDocument.Parse(startResult).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        var result = await _cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "reason": "Environment unavailable"}
            """).RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Environment unavailable", response.GetProperty("data").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Execute_CompletedRun_ReturnsError()
    {
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var runId = JsonDocument.Parse(startResult).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        // Complete the run
        await _engine.FinalizeRunAsync(runId, force: true);

        var result = await _cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_TRANSITION", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_AlreadyCancelledRun_ReturnsError()
    {
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var runId = JsonDocument.Parse(startResult).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        // Cancel once
        await _cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);

        // Try to cancel again
        var result = await _cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_TRANSITION", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ReturnsNextExpectedAction()
    {
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var runId = JsonDocument.Parse(startResult).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        var result = await _cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }
}
