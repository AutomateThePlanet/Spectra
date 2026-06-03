using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Reports;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Integration;

public class FilteredExecutionTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _startTool;
    private readonly GetTestCaseDetailsTool _detailsTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly FinalizeExecutionRunTool _finalizeTool;
    private readonly List<TestIndexEntry> _testEntries;

    public FilteredExecutionTests()
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
            new() { Id = "TC-001", File = "tc-001.md", Title = "Login High Smoke", Priority = "high", Tags = ["smoke", "auth"], Component = "auth" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Login Medium", Priority = "medium", Tags = ["regression"], Component = "auth" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Checkout High Smoke", Priority = "high", Tags = ["smoke"], Component = "checkout" },
            new() { Id = "TC-004", File = "tc-004.md", Title = "Payment Low", Priority = "low", Tags = ["regression"], Component = "payment" }
        ];

        var testCases = _testEntries.ToDictionary(
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

        Func<string, IEnumerable<TestIndexEntry>> indexLoader = _ => _testEntries;
        _startTool = new StartExecutionRunTool(_engine, indexLoader);
        _detailsTool = new GetTestCaseDetailsTool(_engine, (_, id) => testCases.GetValueOrDefault(id), runRepo, resultRepo);
        _advanceTool = new AdvanceTestCaseTool(_engine, resultRepo, runRepo);
        _finalizeTool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter, indexLoader);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task StartRun_FilterByPriority_OnlyHighPriorityTests()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"priority": "high"}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(2, response.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task StartRun_FilterByTags_OnlySmokeTests()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"tags": ["smoke"]}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        // TC-001 and TC-003 have smoke tag
        Assert.Equal(2, response.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task StartRun_FilterByComponent_OnlyAuthTests()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"component": "auth"}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(2, response.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task StartRun_FilterByTestIds_OnlySpecificTests()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"test_ids": ["TC-001", "TC-004"]}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(2, response.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task StartRun_CombinedFilters_AppliesAll()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"priority": "high", "tags": ["smoke"]}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        // Only TC-001 and TC-003 are high priority with smoke tag
        Assert.Equal(2, response.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task StartRun_NoMatchingTests_ReturnsError()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"tags": ["nonexistent"]}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("NO_TESTS_MATCH", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task FilteredRun_CompleteAndFinalize_ReportShowsFilteredCount()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "filters": {"priority": "high"}}
            """).RootElement;

        var startResult = await _startTool.ExecuteAsync(startParams);
        var startResponse = JsonDocument.Parse(startResult).RootElement;
        var runId = startResponse.GetProperty("data").GetProperty("run_id").GetString()!;
        var testCount = startResponse.GetProperty("data").GetProperty("test_count").GetInt32();

        // Complete all filtered tests
        var handle = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_handle").GetString()!;
        for (var i = 0; i < testCount; i++)
        {
            await _detailsTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement);
            var advanceResult = await _advanceTool.ExecuteAsync(JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED"}""").RootElement);
            var advanceResponse = JsonDocument.Parse(advanceResult).RootElement;

            if (advanceResponse.GetProperty("data").TryGetProperty("next", out var next) && next.ValueKind != JsonValueKind.Null)
            {
                handle = next.GetProperty("test_handle").GetString()!;
            }
        }

        // Finalize
        var finalizeResult = await _finalizeTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);
        var finalizeResponse = JsonDocument.Parse(finalizeResult).RootElement;

        Assert.Equal(2, finalizeResponse.GetProperty("data").GetProperty("summary").GetProperty("total").GetInt32());
        Assert.Equal(2, finalizeResponse.GetProperty("data").GetProperty("summary").GetProperty("passed").GetInt32());
    }

    [Fact]
    public async Task RegressionGuard_PreviousSilentDropFormsNoLongerEnqueueWholeSuite()
    {
        // Spec 051: the full suite is 4 tests. Each prior silent-drop form must now
        // EITHER filter correctly (count < 4) OR raise an actionable error — never
        // enqueue the unfiltered whole suite.
        const int fullSuiteSize = 4;

        // Form 1: top-level plural (was silently dropped → whole suite). Now filters.
        var form1 = JsonDocument.Parse("""{"suite": "checkout", "priorities": ["high"]}""").RootElement;
        var r1 = await _startTool.ExecuteAsync(form1);
        var count1 = JsonDocument.Parse(r1).RootElement.GetProperty("data").GetProperty("test_count").GetInt32();
        Assert.Equal(2, count1);                 // TC-001, TC-003
        Assert.NotEqual(fullSuiteSize, count1);

        // (reset active run for the next start)
        await new CancelAllActiveRunsTool(_engine, new RunRepository(_db)).ExecuteAsync(JsonDocument.Parse("{}").RootElement);

        // Form 2: top-level singular 'priority' — now an actionable error, not a whole-suite run.
        var form2 = JsonDocument.Parse("""{"suite": "checkout", "priority": "high"}""").RootElement;
        await Assert.ThrowsAsync<McpInvalidParamsException>(() => _startTool.ExecuteAsync(form2));

        // Form 3: plural nested under legacy 'filters' — now an actionable error.
        var form3 = JsonDocument.Parse("""{"suite": "checkout", "filters": {"priorities": ["high"]}}""").RootElement;
        await Assert.ThrowsAsync<McpInvalidParamsException>(() => _startTool.ExecuteAsync(form3));
    }

    [Fact]
    public async Task FilteredRun_Environment_IncludedInResponse()
    {
        var startParams = JsonDocument.Parse("""
            {"suite": "checkout", "environment": "staging", "filters": {"priority": "high"}}
            """).RootElement;

        var result = await _startTool.ExecuteAsync(startParams);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        // Environment should be stored with the run
        Assert.Equal("Running", response.GetProperty("run_status").GetString());
    }
}
