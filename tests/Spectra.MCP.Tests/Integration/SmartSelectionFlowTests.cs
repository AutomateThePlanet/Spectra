using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Reports;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.Data;
using Spectra.MCP.Tools.RunManagement;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Integration;

public class SmartSelectionFlowTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly FindTestCasesTool _findTool;
    private readonly StartExecutionRunTool _startTool;
    private readonly AdvanceTestCaseTool _advanceTool;
    private readonly FinalizeExecutionRunTool _finalizeTool;
    private readonly ListSavedSelectionsTool _listSelectionsTool;
    private readonly GetTestExecutionHistoryTool _historyTool;

    private readonly Dictionary<string, List<TestIndexEntry>> _suiteIndexes;

    public SmartSelectionFlowTests()
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

        _suiteIndexes = new Dictionary<string, List<TestIndexEntry>>
        {
            ["auth"] =
            [
                new() { Id = "TC-001", File = "tc-001.md", Title = "Login with valid credentials", Priority = "high", Tags = ["smoke", "auth"], Component = "auth", Description = "Verify login flow" },
                new() { Id = "TC-002", File = "tc-002.md", Title = "Login with invalid password", Priority = "high", Tags = ["auth", "negative"], Component = "auth" },
                new() { Id = "TC-003", File = "tc-003.md", Title = "Password reset email", Priority = "medium", Tags = ["auth"], Component = "auth" }
            ],
            ["checkout"] =
            [
                new() { Id = "TC-101", File = "tc-101.md", Title = "Add item to cart", Priority = "high", Tags = ["smoke", "checkout"], Component = "checkout" },
                new() { Id = "TC-102", File = "tc-102.md", Title = "Apply discount code", Priority = "medium", Tags = ["checkout"], Component = "checkout" },
                new() { Id = "TC-103", File = "tc-103.md", Title = "Payment with expired card", Priority = "high", Tags = ["checkout", "negative", "payment"], Component = "payment" }
            ]
        };

        Func<IEnumerable<string>> suiteListLoader = () => _suiteIndexes.Keys;
        Func<string, IEnumerable<TestIndexEntry>> indexLoader = suite =>
            _suiteIndexes.TryGetValue(suite, out var entries) ? entries : [];

        var selections = new Dictionary<string, SavedSelectionConfig>
        {
            ["smoke"] = new() { Description = "Smoke tests", Priorities = ["high"], Tags = ["smoke"] },
            ["auth-full"] = new() { Description = "All auth tests", Components = ["auth"] }
        };
        Func<IReadOnlyDictionary<string, SavedSelectionConfig>> selectionsLoader = () => selections;

        _findTool = new FindTestCasesTool(suiteListLoader, indexLoader);
        _startTool = new StartExecutionRunTool(_engine, indexLoader, suiteListLoader, selectionsLoader);
        _advanceTool = new AdvanceTestCaseTool(_engine, resultRepo, runRepo);
        _finalizeTool = new FinalizeExecutionRunTool(_engine, reportGenerator, reportWriter, indexLoader);
        _listSelectionsTool = new ListSavedSelectionsTool(selectionsLoader, suiteListLoader, indexLoader);
        _historyTool = new GetTestExecutionHistoryTool(resultRepo);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task FindTests_ThenStartRunWithTestIds_StartsCorrectRun()
    {
        // Step 1: Find payment-related tests
        var findResult = await _findTool.ExecuteAsync(
            JsonDocument.Parse("""{"query": "payment"}""").RootElement);
        var findResponse = JsonDocument.Parse(findResult).RootElement;
        Assert.Equal(1, findResponse.GetProperty("data").GetProperty("matched").GetInt32());

        // Step 2: Also find smoke tests
        var smokeResult = await _findTool.ExecuteAsync(
            JsonDocument.Parse("""{"tags": ["smoke"]}""").RootElement);
        var smokeResponse = JsonDocument.Parse(smokeResult).RootElement;
        Assert.Equal(2, smokeResponse.GetProperty("data").GetProperty("matched").GetInt32());

        // Step 3: Start run with specific test IDs from search results
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"test_ids": ["TC-001", "TC-103"], "name": "Payment + Login Smoke"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        // Verify run was created with correct tests
        Assert.Equal(2, startResponse.GetProperty("data").GetProperty("test_count").GetInt32());
        Assert.NotNull(startResponse.GetProperty("data").GetProperty("run_id").GetString());

        // First test should be one of the requested IDs
        var firstTestId = startResponse.GetProperty("data").GetProperty("first_test").GetProperty("test_id").GetString();
        Assert.Contains(firstTestId, new[] { "TC-001", "TC-103" });
    }

    [Fact]
    public async Task ListSelections_ThenStartRunBySelection_ExecutesCorrectTests()
    {
        // Step 1: List saved selections
        var listResult = await _listSelectionsTool.ExecuteAsync(null);
        var listResponse = JsonDocument.Parse(listResult).RootElement;

        var selections = listResponse.GetProperty("data").GetProperty("selections");
        Assert.Equal(2, selections.GetArrayLength());

        // Step 2: Start run with "smoke" selection
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"selection": "smoke", "name": "Smoke Run"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        // smoke selection: priorities=["high"] AND tags=["smoke"] → TC-001, TC-101 (high + smoke)
        Assert.Equal(2, startResponse.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task SelectionWithComponentFilter_MatchesCorrectTests()
    {
        // Start run with "auth-full" selection (components=["auth"])
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"selection": "auth-full", "name": "Auth Full Run"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        // auth-full: components=["auth"] → TC-001, TC-002, TC-003
        Assert.Equal(3, startResponse.GetProperty("data").GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task TestIdsFromMultipleSuites_RunSpansSuites()
    {
        // Pick tests from both suites
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"test_ids": ["TC-001", "TC-002", "TC-101", "TC-103"], "name": "Cross-Suite Run"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        Assert.Equal(4, startResponse.GetProperty("data").GetProperty("test_count").GetInt32());
        Assert.Contains("+", startResponse.GetProperty("data").GetProperty("suite").GetString());
    }

    [Fact]
    public async Task InvalidTestIds_ReturnsError()
    {
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"test_ids": ["TC-001", "TC-INVALID"], "name": "Bad Run"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        Assert.Equal("INVALID_TEST_IDS", startResponse.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task NonexistentSelection_ReturnsError()
    {
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"selection": "nonexistent", "name": "Bad Run"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        Assert.Equal("SELECTION_NOT_FOUND", startResponse.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MutualExclusivity_ReturnsError()
    {
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"suite": "auth", "test_ids": ["TC-001"], "name": "Bad Run"}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        Assert.Equal("INVALID_PARAMS", startResponse.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TestIdsWithoutName_ReturnsError()
    {
        var startResult = await _startTool.ExecuteAsync(
            JsonDocument.Parse("""{"test_ids": ["TC-001"]}""").RootElement);
        var startResponse = JsonDocument.Parse(startResult).RootElement;

        Assert.Equal("INVALID_PARAMS", startResponse.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task FindTests_CombinedFilters_CorrectResults()
    {
        // AND between priorities and tags
        var findResult = await _findTool.ExecuteAsync(
            JsonDocument.Parse("""{"priorities": ["high"], "tags": ["negative"]}""").RootElement);
        var findResponse = JsonDocument.Parse(findResult).RootElement;

        // high + negative → TC-002 (high, negative), TC-103 (high, negative)
        Assert.Equal(2, findResponse.GetProperty("data").GetProperty("matched").GetInt32());
    }

    [Fact]
    public async Task FindTests_QueryMatchesDescription()
    {
        // "login flow" should match TC-001 which has description "Verify login flow"
        var findResult = await _findTool.ExecuteAsync(
            JsonDocument.Parse("""{"query": "login flow"}""").RootElement);
        var findResponse = JsonDocument.Parse(findResult).RootElement;

        var tests = findResponse.GetProperty("data").GetProperty("tests");
        Assert.True(tests.GetArrayLength() >= 1);

        // TC-001 should be ranked highest (matches both "login" in title and "flow" in description)
        var firstTest = tests[0];
        Assert.Equal("TC-001", firstTest.GetProperty("id").GetString());
    }

    [Fact]
    public async Task ExecutionHistory_EmptyForNewTests()
    {
        var historyResult = await _historyTool.ExecuteAsync(
            JsonDocument.Parse("""{"test_ids": ["TC-001"]}""").RootElement);
        var historyResponse = JsonDocument.Parse(historyResult).RootElement;

        // Should succeed even with no history
        Assert.DoesNotContain("error", historyResult);
    }
}
