using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Reports;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class FinalizeExecutionRunTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly FinalizeExecutionRunTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public FinalizeExecutionRunTests()
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
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = [] },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = [] }
        ];

        _tool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private async Task<string> StartAndCompleteAllTests()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);

        // Make a copy of handles to avoid modifying during iteration
        var handles = queue.Tests.Select(t => t.TestHandle).ToList();
        foreach (var handle in handles)
        {
            await _engine.StartTestAsync(run.RunId, handle);
            await _engine.AdvanceTestAsync(run.RunId, handle, TestStatus.Passed);
        }

        return run.RunId;
    }

    [Fact]
    public async Task Execute_AllTestsComplete_ReturnsCompletedStatus()
    {
        var runId = await StartAndCompleteAllTests();
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Completed", response.GetProperty("run_status").GetString());
        Assert.Equal("2/2", response.GetProperty("progress").GetString());
        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_AllTestsComplete_GeneratesReport()
    {
        var runId = await StartAndCompleteAllTests();
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var data = response.GetProperty("data");
        Assert.True(data.TryGetProperty("report_path", out var reportPath));
        Assert.True(File.Exists(reportPath.GetString()));
    }

    [Fact]
    public async Task Execute_AllTestsComplete_ReturnsSummary()
    {
        var runId = await StartAndCompleteAllTests();
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var summary = response.GetProperty("data").GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("total").GetInt32());
        Assert.Equal(2, summary.GetProperty("passed").GetInt32());
        Assert.Equal(0, summary.GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task Execute_TestsPending_ReturnsError()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", _testEntries);
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{run.RunId}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("TESTS_PENDING", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_TestsPendingWithForce_Succeeds()
    {
        var (run, _) = await _engine.StartRunAsync("checkout", _testEntries);
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{run.RunId}}}", "force": true}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Completed", response.GetProperty("run_status").GetString());
    }

    [Fact]
    public async Task Execute_InvalidRunId_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{"run_id": "nonexistent"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("RUN_NOT_FOUND", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MissingRunId_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_PARAMS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_GeneratesBothJsonAndMarkdownReports()
    {
        var runId = await StartAndCompleteAllTests();
        var parameters = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;

        await _tool.ExecuteAsync(parameters);

        var reportsDir = Path.Combine(_testDir, "reports");
        Assert.True(File.Exists(Path.Combine(reportsDir, $"{runId}.json")));
        Assert.True(File.Exists(Path.Combine(reportsDir, $"{runId}.md")));
    }
}
