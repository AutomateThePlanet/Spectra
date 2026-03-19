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

public class PauseResumeTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly PauseExecutionRunTool _pauseTool;
    private readonly ResumeExecutionRunTool _resumeTool;
    private readonly GetTestCaseDetailsTool _detailsTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly FinalizeExecutionRunTool _finalizeTool;
    private readonly List<TestIndexEntry> _testEntries;

    public PauseResumeTests()
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

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = ["regression"] },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low", Tags = ["smoke"] }
        ];

        var testCases = new Dictionary<string, TestCase>
        {
            ["TC-001"] = new() { Id = "TC-001", FilePath = "tc-001.md", Title = "Test One", Priority = Priority.High, Steps = ["Step 1"], ExpectedResult = "Expected 1" },
            ["TC-002"] = new() { Id = "TC-002", FilePath = "tc-002.md", Title = "Test Two", Priority = Priority.Medium, Steps = ["Step 1"], ExpectedResult = "Expected 2" },
            ["TC-003"] = new() { Id = "TC-003", FilePath = "tc-003.md", Title = "Test Three", Priority = Priority.Low, Steps = ["Step 1"], ExpectedResult = "Expected 3" }
        };

        Func<string, IEnumerable<TestIndexEntry>> indexLoader = _ => _testEntries;
        _startTool = new StartExecutionRunTool(_engine, indexLoader);
        _pauseTool = new PauseExecutionRunTool(_engine);
        _resumeTool = new ResumeExecutionRunTool(_engine);
        _detailsTool = new GetTestCaseDetailsTool(_engine, (_, id) => testCases.GetValueOrDefault(id));
        _advanceTool = new AdvanceTestCaseTool(_engine);
        _finalizeTool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter, indexLoader);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task FullPauseResumeFlow_PreservesStateAndProgress()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        Assert.Equal("Running", startResponse.GetProperty("run_status").GetString());
        Assert.Equal("0/3", startResponse.GetProperty("progress").GetString());

        // Get and complete first test
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        var advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "PASSED"}""").RootElement;
        var advanceResult = await _advanceTool.ExecuteAsync(advanceParams);
        var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

        Assert.Equal("1/3", advanceResponse.GetProperty("progress").GetString());

        // Pause
        var pauseParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        var pauseResult = await _pauseTool.ExecuteAsync(pauseParams);
        var pauseResponse = JsonDocument.Parse(pauseResult).RootElement;

        Assert.Equal("Paused", pauseResponse.GetProperty("run_status").GetString());
        Assert.Equal("1/3", pauseResponse.GetProperty("progress").GetString());

        // Resume
        var resumeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        var resumeResult = await _resumeTool.ExecuteAsync(resumeParams);
        var resumeResponse = JsonDocument.Parse(resumeResult).RootElement;

        Assert.Equal("Running", resumeResponse.GetProperty("run_status").GetString());
        Assert.Equal("1/3", resumeResponse.GetProperty("progress").GetString());

        // Next test should be TC-002
        var nextTest = resumeResponse.GetProperty("data").GetProperty("next_test");
        Assert.Equal("TC-002", nextTest.GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task MultiplePauseResume_MaintainsCorrectState()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        // Pause immediately
        var pauseParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        await _pauseTool.ExecuteAsync(pauseParams);

        // Resume
        var resumeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        var resumeResult = await _resumeTool.ExecuteAsync(resumeParams);
        var resumeResponse = JsonDocument.Parse(resumeResult).RootElement;

        Assert.Equal("Running", resumeResponse.GetProperty("run_status").GetString());

        // Complete a test
        var nextHandle = resumeResponse.GetProperty("data").GetProperty("next_test").GetProperty("test_handle").GetString()!;
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        var advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}", "status": "PASSED"}""").RootElement;
        await _advanceTool.ExecuteAsync(advanceParams);

        // Pause again
        var pause2Result = await _pauseTool.ExecuteAsync(pauseParams);
        var pause2Response = JsonDocument.Parse(pause2Result).RootElement;

        Assert.Equal("Paused", pause2Response.GetProperty("run_status").GetString());
        Assert.Equal("1/3", pause2Response.GetProperty("progress").GetString());

        // Resume again
        var resume2Result = await _resumeTool.ExecuteAsync(resumeParams);
        var resume2Response = JsonDocument.Parse(resume2Result).RootElement;

        Assert.Equal("Running", resume2Response.GetProperty("run_status").GetString());
        Assert.Equal("TC-002", resume2Response.GetProperty("data").GetProperty("next_test").GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task PauseResume_ThenComplete_GeneratesReport()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        // Get first test handle
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // Complete first test
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);
        var advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "PASSED"}""").RootElement;
        var advanceResult = await _advanceTool.ExecuteAsync(advanceParams);
        var nextHandle = JsonDocument.Parse(advanceResult).RootElement.GetProperty("data").GetProperty("next").GetProperty("test_handle").GetString()!;

        // Pause
        var pauseParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        await _pauseTool.ExecuteAsync(pauseParams);

        // Resume
        var resumeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        await _resumeTool.ExecuteAsync(resumeParams);

        // Complete remaining tests
        for (var i = 0; i < 2; i++)
        {
            detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}"}""").RootElement;
            await _detailsTool.ExecuteAsync(detailsParams);

            advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}", "status": "PASSED"}""").RootElement;
            advanceResult = await _advanceTool.ExecuteAsync(advanceParams);
            var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

            if (advanceResponse.GetProperty("data").TryGetProperty("next", out var next) && next.ValueKind != JsonValueKind.Null)
            {
                nextHandle = next.GetProperty("test_handle").GetString()!;
            }
        }

        // Finalize
        var finalizeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        var finalizeResult = await _finalizeTool.ExecuteAsync(finalizeParams);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        Assert.Equal("Completed", finalizeResponse.GetProperty("run_status").GetString());
        Assert.Equal(3, finalizeResponse.GetProperty("data").GetProperty("summary").GetProperty("passed").GetInt32());
    }
}
