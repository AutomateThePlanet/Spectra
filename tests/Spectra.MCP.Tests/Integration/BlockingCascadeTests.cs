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

public class BlockingCascadeTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly GetTestCaseDetailsTool _detailsTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly SkipTestCaseTool _skipTool;
    private readonly FinalizeExecutionRunTool _finalizeTool;
    private readonly List<TestIndexEntry> _testEntries;

    public BlockingCascadeTests()
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
            new() { Id = "TC-001", File = "tc-001.md", Title = "Login Test", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Checkout Test", Priority = "high", DependsOn = "TC-001" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Payment Test", Priority = "medium", DependsOn = "TC-002" },
            new() { Id = "TC-004", File = "tc-004.md", Title = "Logout Test", Priority = "low" }
        ];

        var testCases = new Dictionary<string, TestCase>
        {
            ["TC-001"] = new() { Id = "TC-001", FilePath = "tc-001.md", Title = "Login Test", Priority = Priority.High, Steps = ["Step 1"], ExpectedResult = "Result 1" },
            ["TC-002"] = new() { Id = "TC-002", FilePath = "tc-002.md", Title = "Checkout Test", Priority = Priority.High, Steps = ["Step 1"], ExpectedResult = "Result 2" },
            ["TC-003"] = new() { Id = "TC-003", FilePath = "tc-003.md", Title = "Payment Test", Priority = Priority.Medium, Steps = ["Step 1"], ExpectedResult = "Result 3" },
            ["TC-004"] = new() { Id = "TC-004", FilePath = "tc-004.md", Title = "Logout Test", Priority = Priority.Low, Steps = ["Step 1"], ExpectedResult = "Result 4" }
        };

        _startTool = new StartExecutionRunTool(_engine, _ => _testEntries);
        _detailsTool = new GetTestCaseDetailsTool(_engine, (_, id) => testCases.GetValueOrDefault(id));
        _advanceTool = new AdvanceTestCaseTool(_engine);
        _skipTool = new SkipTestCaseTool(_engine);
        _finalizeTool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task FailedTest_BlocksDependents_TransitiveChain()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        // Get first test (TC-001)
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        // Fail TC-001
        var advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "FAILED", "notes": "Login button missing"}""").RootElement;
        var advanceResult = await _advanceTool.ExecuteAsync(advanceParams);
        var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

        // Verify TC-002 and TC-003 are blocked
        var blocked = advanceResponse.GetProperty("data").GetProperty("blocked_tests");
        Assert.Contains("TC-002", blocked.EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("TC-003", blocked.EnumerateArray().Select(e => e.GetString()));

        // Next test should be TC-004 (independent)
        var nextTest = advanceResponse.GetProperty("data").GetProperty("next");
        Assert.Equal("TC-004", nextTest.GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task SkippedTest_BlocksDependents()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        // Get first test
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        // Skip TC-001
        var skipParams = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstHandle}}}", "reason": "Environment not ready"}
            """).RootElement;
        var skipResult = await _skipTool.ExecuteAsync(skipParams);
        var skipResponse = JsonDocument.Parse(skipResult).RootElement;

        // Verify dependents are blocked
        var blocked = skipResponse.GetProperty("data").GetProperty("blocked_tests");
        Assert.True(blocked.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Report_IncludesBlockedTests()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;

        // Get and fail first test
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement);
        var advanceResult = await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "FAILED"}""").RootElement);

        // Get and pass independent test (TC-004)
        var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;
        var nextHandle = advanceResponse.GetProperty("data").GetProperty("next").GetProperty("test_handle").GetString()!;
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}"}""").RootElement);
        await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{nextHandle}}}", "status": "PASSED"}""").RootElement);

        // Finalize
        var finalizeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}", "force": true}""").RootElement;
        var finalizeResult = await _finalizeTool.ExecuteAsync(finalizeParams);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        // Verify summary
        var summary = finalizeResponse.GetProperty("data").GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("failed").GetInt32());
        Assert.Equal(1, summary.GetProperty("passed").GetInt32());
        Assert.Equal(2, summary.GetProperty("blocked").GetInt32());
    }

    [Fact]
    public async Task IndependentTests_NotBlocked()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        // Get and fail first test
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;
        await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}"}""").RootElement);
        var advanceResult = await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{firstHandle}}}", "status": "FAILED"}""").RootElement);
        var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

        // TC-004 should not be blocked (no dependency on TC-001)
        var blocked = advanceResponse.GetProperty("data").GetProperty("blocked_tests");
        Assert.DoesNotContain("TC-004", blocked.EnumerateArray().Select(e => e.GetString()));

        // Next test should be TC-004
        var nextTest = advanceResponse.GetProperty("data").GetProperty("next");
        Assert.Equal("TC-004", nextTest.GetProperty("test_id").GetString());
    }
}
