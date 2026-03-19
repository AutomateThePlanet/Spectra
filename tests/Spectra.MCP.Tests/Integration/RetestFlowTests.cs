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

namespace Spectra.MCP.Tests.Integration;

public class RetestFlowTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly GetTestCaseDetailsTool _detailsTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly RetestTestCaseTool _retestTool;
    private readonly FinalizeExecutionRunTool _finalizeTool;

    public RetestFlowTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir, ReportsPath = Path.Combine(_testDir, "reports") };

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);

        var reportGenerator = new ReportGenerator();
        var reportWriter = new ReportWriter(config.ReportsPath);

        var testEntries = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium" }
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

        Func<string, IEnumerable<TestIndexEntry>> indexLoader = _ => testEntries;
        _startTool = new StartExecutionRunTool(_engine, indexLoader);
        _detailsTool = new GetTestCaseDetailsTool(_engine, (_, id) => testCases.GetValueOrDefault(id));
        _advanceTool = new AdvanceTestCaseTool(_engine);
        _retestTool = new RetestTestCaseTool(_engine);
        _finalizeTool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter, indexLoader);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task RetestFlow_FailThenRetest_ShowsBothAttempts()
    {
        // Start a run
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var handle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Fail first test
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement);
        var advanceResult = await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "FAILED", "notes": "Test failed"}""").RootElement);
        var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;
        handle = advanceResponse.GetProperty("data").GetProperty("next").GetProperty("test_handle").GetString()!;

        // Pass second test
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED"}""").RootElement);

        // Retest first test
        var retestResult = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);
        var retestResponse = JsonDocument.Parse(retestResult).RootElement;
        var retestHandle = retestResponse.GetProperty("data").GetProperty("test_handle").GetString()!;

        // Pass the retested test
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{retestHandle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{retestHandle}}}", "status": "PASSED"}""").RootElement);

        // Finalize
        var finalizeResult = await _finalizeTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        // Summary should count the retest
        var summary = finalizeResponse.GetProperty("data").GetProperty("summary");
        Assert.Equal(3, summary.GetProperty("total").GetInt32()); // 2 original + 1 retest
        Assert.Equal(2, summary.GetProperty("passed").GetInt32()); // TC-002 passed, TC-001 retest passed
        Assert.Equal(1, summary.GetProperty("failed").GetInt32()); // TC-001 original failed
    }

    [Fact]
    public async Task RetestFlow_MultipleRetests_AllTracked()
    {
        // Start a run
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var handle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Fail first test
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "FAILED", "notes": "Test failed"}""").RootElement);

        // Retest first time - fail again
        var retestResult1 = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);
        var retest1Handle = JsonDocument.Parse(retestResult1).RootElement.GetProperty("data").GetProperty("test_handle").GetString()!;

        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{retest1Handle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{retest1Handle}}}", "status": "FAILED", "notes": "Test failed again"}""").RootElement);

        // Retest second time - pass
        var retestResult2 = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);
        var retest2Handle = JsonDocument.Parse(retestResult2).RootElement.GetProperty("data").GetProperty("test_handle").GetString()!;

        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{retest2Handle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{retest2Handle}}}", "status": "PASSED"}""").RootElement);

        // Complete second test
        var status = await _engine.GetStatusAsync(runId);
        var nextHandle = status!.Value.Queue.GetNext()!.TestHandle;
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}", "status": "PASSED"}""").RootElement);

        // Finalize
        var finalizeResult = await _finalizeTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        // Summary should count all attempts
        var summary = finalizeResponse.GetProperty("data").GetProperty("summary");
        Assert.Equal(4, summary.GetProperty("total").GetInt32()); // 2 original + 2 retests
        Assert.Equal(2, summary.GetProperty("passed").GetInt32()); // TC-002 passed, TC-001 retest2 passed
        Assert.Equal(2, summary.GetProperty("failed").GetInt32()); // TC-001 original failed, TC-001 retest1 failed
    }

    [Fact]
    public async Task RetestFlow_RetestAddsToProgress()
    {
        // Start a run
        var startResult = await _startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "test"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var handle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Initial progress should be 0/2
        Assert.Equal("0/2", startResponse.GetProperty("progress").GetString());

        // Fail first test
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement);
        var advanceResult = await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "FAILED", "notes": "Test failed"}""").RootElement);

        // Progress should be 1/2
        Assert.Equal("1/2", JsonDocument.Parse(advanceResult).RootElement.GetProperty("progress").GetString());

        // Retest
        var retestResult = await _retestTool.ExecuteAsync(JsonDocument.Parse($$$"""
            {"run_id": "{{{runId}}}", "test_id": "TC-001"}
            """).RootElement);

        // Progress should show 1/3 (1 completed out of new total 3)
        Assert.Equal("1/3", JsonDocument.Parse(retestResult).RootElement.GetProperty("progress").GetString());
    }
}
