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

public class ExecutionFlowTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly GetTestCaseDetailsTool _detailsTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly FinalizeExecutionRunTool _finalizeTool;
    private readonly List<TestIndexEntry> _testEntries;

    public ExecutionFlowTests()
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
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Login Test", Priority = "high", Tags = ["smoke"] },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Checkout Test", Priority = "high", Tags = ["smoke"], DependsOn = "TC-001" },
            new TestIndexEntry { Id = "TC-003", File = "tc-003.md", Title = "Payment Test", Priority = "medium", Tags = ["regression"], DependsOn = "TC-002" },
            new TestIndexEntry { Id = "TC-004", File = "tc-004.md", Title = "Logout Test", Priority = "low", Tags = ["smoke"] }
        ];

        var testCases = new Dictionary<string, TestCase>
        {
            ["TC-001"] = new() { Id = "TC-001", FilePath = "tc-001.md", Title = "Login Test", Priority = Priority.High, Tags = ["smoke"], Steps = ["Open login", "Enter creds", "Click login"], ExpectedResult = "User logged in" },
            ["TC-002"] = new() { Id = "TC-002", FilePath = "tc-002.md", Title = "Checkout Test", Priority = Priority.High, Tags = ["smoke"], Steps = ["Add item", "Go to cart", "Checkout"], ExpectedResult = "Order placed" },
            ["TC-003"] = new() { Id = "TC-003", FilePath = "tc-003.md", Title = "Payment Test", Priority = Priority.Medium, Tags = ["regression"], Steps = ["Select payment", "Enter card", "Confirm"], ExpectedResult = "Payment processed" },
            ["TC-004"] = new() { Id = "TC-004", FilePath = "tc-004.md", Title = "Logout Test", Priority = Priority.Low, Tags = ["smoke"], Steps = ["Click logout"], ExpectedResult = "User logged out" }
        };

        Func<string, IEnumerable<TestIndexEntry>> indexLoader = _ => _testEntries;
        _startTool = new StartExecutionRunTool(_engine, indexLoader);
        _detailsTool = new GetTestCaseDetailsTool(_engine, (_, id) => testCases.GetValueOrDefault(id), runRepo, resultRepo);
        _advanceTool = new AdvanceTestCaseTool(_engine, resultRepo, runRepo);
        _finalizeTool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter, indexLoader);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task FullExecutionFlow_AllPassed_GeneratesReport()
    {
        // 1. Start run
        var startParams = JsonDocument.Parse("""{"suite": "e2e"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        Assert.Equal("Running", startResponse.GetProperty("run_status").GetString());
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var firstHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // 2. Execute each test
        var currentHandle = firstHandle;
        var testCount = 0;

        while (currentHandle != null)
        {
            // Get details
            var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{currentHandle}}}"}""").RootElement;
            var detailsResult = await _detailsTool.ExecuteAsync(detailsParams);
            var detailsResponse = JsonDocument.Parse(detailsResult).RootElement;

            Assert.True(detailsResponse.TryGetProperty("data", out _));

            // Advance with PASSED
            var advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{currentHandle}}}", "status": "PASSED"}""").RootElement;
            var advanceResult = await _advanceTool.ExecuteAsync(advanceParams);
            var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

            testCount++;

            var next = advanceResponse.GetProperty("data").GetProperty("next");
            currentHandle = next.ValueKind == JsonValueKind.Null ? null : next.GetProperty("test_handle").GetString();
        }

        Assert.Equal(4, testCount);

        // 3. Finalize
        var finalizeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        var finalizeResult = await _finalizeTool.ExecuteAsync(finalizeParams);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        Assert.Equal("Completed", finalizeResponse.GetProperty("run_status").GetString());
        var summary = finalizeResponse.GetProperty("data").GetProperty("summary");
        Assert.Equal(4, summary.GetProperty("total").GetInt32());
        Assert.Equal(4, summary.GetProperty("passed").GetInt32());
    }

    [Fact]
    public async Task FullExecutionFlow_WithFailure_BlocksDependents()
    {
        // 1. Start run
        var startParams = JsonDocument.Parse("""{"suite": "e2e"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var currentHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // 2. Get details for TC-001
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{currentHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        // 3. Fail TC-001 - should block TC-002 and TC-003 (transitive)
        var advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{currentHandle}}}", "status": "FAILED", "notes": "Login button broken"}""").RootElement;
        var advanceResult = await _advanceTool.ExecuteAsync(advanceParams);
        var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

        var blocked = advanceResponse.GetProperty("data").GetProperty("blocked_tests");
        Assert.Contains("TC-002", blocked.EnumerateArray().Select(e => e.GetString()));

        // 4. Continue with TC-004 (not dependent on TC-001)
        var next = advanceResponse.GetProperty("data").GetProperty("next");
        Assert.NotEqual(JsonValueKind.Null, next.ValueKind);
        currentHandle = next.GetProperty("test_handle").GetString()!;

        // Get details and pass TC-004
        detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{currentHandle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        advanceParams = JsonDocument.Parse($$$"""{"test_handle": "{{{currentHandle}}}", "status": "PASSED"}""").RootElement;
        await _advanceTool.ExecuteAsync(advanceParams);

        // 5. Finalize with force (some tests blocked)
        var finalizeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}", "force": true}""").RootElement;
        var finalizeResult = await _finalizeTool.ExecuteAsync(finalizeParams);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        var summary = finalizeResponse.GetProperty("data").GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("failed").GetInt32());
        Assert.Equal(1, summary.GetProperty("passed").GetInt32());
        Assert.True(summary.GetProperty("blocked").GetInt32() >= 1);
    }

    [Fact]
    public async Task FullExecutionFlow_MixedResults_ReportsAccurately()
    {
        // Start run
        var startParams = JsonDocument.Parse("""{"suite": "e2e"}""").RootElement;
        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var currentHandle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;

        // TC-001: PASSED
        await ExecuteTest(currentHandle, "PASSED");
        currentHandle = await GetNextHandle(currentHandle);

        // TC-002: PASSED
        await ExecuteTest(currentHandle!, "PASSED");
        currentHandle = await GetNextHandle(currentHandle!);

        // TC-003: FAILED
        await ExecuteTest(currentHandle!, "FAILED");
        currentHandle = await GetNextHandle(currentHandle!);

        // TC-004: PASSED
        await ExecuteTest(currentHandle!, "PASSED");

        // Finalize
        var finalizeParams = JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement;
        var finalizeResult = await _finalizeTool.ExecuteAsync(finalizeParams);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        var summary = finalizeResponse.GetProperty("data").GetProperty("summary");
        Assert.Equal(4, summary.GetProperty("total").GetInt32());
        Assert.Equal(3, summary.GetProperty("passed").GetInt32());
        Assert.Equal(1, summary.GetProperty("failed").GetInt32());
    }

    private async Task ExecuteTest(string handle, string status)
    {
        var detailsParams = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement;
        await _detailsTool.ExecuteAsync(detailsParams);

        // Notes are required for FAILED status
        var json = status == "FAILED"
            ? $$$"""{"test_handle": "{{{handle}}}", "status": "{{{status}}}", "notes": "Test failed"}"""
            : $$$"""{"test_handle": "{{{handle}}}", "status": "{{{status}}}"}""";
        var advanceParams = JsonDocument.Parse(json).RootElement;
        await _advanceTool.ExecuteAsync(advanceParams);
    }

    private async Task<string?> GetNextHandle(string currentHandle)
    {
        var result = await _engine.GetTestResultAsync(currentHandle);
        var queue = await _engine.GetQueueAsync(result!.RunId);
        var next = queue?.GetNext();
        return next?.TestHandle;
    }
}
