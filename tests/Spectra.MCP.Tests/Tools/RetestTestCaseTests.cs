using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Reports;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Tools;

public class RetestTestCaseTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly GetTestCaseDetailsTool _detailsTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly RetestTestCaseTool _retestTool;

    public RetestTestCaseTests()
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
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low" }
        };

        var testCases = testEntries.ToDictionary(
            e => e.Id,
            e => new TestCase
            {
                Id = e.Id,
                FilePath = e.File,
                Title = e.Title,
                Priority = Enum.Parse<Priority>(e.Priority, true),
                Steps = ["Step 1"],
                ExpectedResult = "Expected result"
            });

        _startTool = new StartExecutionRunTool(_engine, _ => testEntries);
        _detailsTool = new GetTestCaseDetailsTool(_engine, (_, id) => testCases.GetValueOrDefault(id));
        _advanceTool = new AdvanceTestCaseTool(_engine);
        _retestTool = new RetestTestCaseTool(_engine);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_InvalidTestId_ReturnsError()
    {
        // Start a run
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        var result = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "NONEXISTENT"}
            """).RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("TEST_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_TestNotCompleted_ReturnsError()
    {
        // Start a run
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        // Try to retest a test that hasn't been executed
        var result = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-002"}
            """).RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("TEST_NOT_COMPLETED", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_FailedTest_RequeuesWithNewHandle()
    {
        // Start a run and fail the first test
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Get details and fail it
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "FAILED"}""").RootElement);

        // Retest the failed test
        var result = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal("TC-001", data.GetProperty("test_id").GetString());
        var newHandle = data.GetProperty("test_handle").GetString()!;
        Assert.NotEqual(firstHandle, newHandle);
    }

    [Fact]
    public async Task Execute_RequeuesAtEndOfQueue()
    {
        // Start a run
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Fail first test
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "FAILED"}""").RootElement);

        // Retest first test - should be requeued at end
        var retestResult = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);
        var retestResponse = JsonDocument.Parse(retestResult).RootElement;

        // Progress should show 1/4 (1 completed, 4 total - original 3 plus requeued)
        Assert.Equal("1/4", retestResponse.GetProperty("progress").GetString());
    }

    [Fact]
    public async Task Execute_InvalidRunId_ReturnsError()
    {
        var result = await _retestTool.ExecuteAsync(JsonDocument.Parse("""
            {"run_id": "nonexistent", "test_id": "TC-001"}
            """).RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("RUN_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ReturnsRunningStatus()
    {
        // Start a run and fail the first test
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Get details and fail it
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "FAILED"}""").RootElement);

        // Retest the failed test
        var result = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Running", response.GetProperty("run_status").GetString());
    }
}
