using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class StartExecutionRunTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly StartExecutionRunTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public StartExecutionRunTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir };

        _engine = new ExecutionEngine(runRepo, resultRepo, new QueueSnapshotRepository(_db), identity, config);

        _testEntries =
        [
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = ["regression"] },
            new TestIndexEntry { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low", Tags = ["smoke", "regression"] }
        ];

        _tool = new StartExecutionRunTool(_engine, suite => _testEntries);
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
    public async Task Execute_ValidSuite_ReturnsRunWithFirstTest()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("run_id", out _));
        Assert.Equal("checkout", data.GetProperty("suite").GetString());
        Assert.Equal(3, data.GetProperty("test_count").GetInt32());
        Assert.True(data.TryGetProperty("first_test", out var firstTest));
        Assert.Equal("TC-001", firstTest.GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task Execute_ValidSuite_ReturnsRunningStatus()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Running", response.GetProperty("run_status").GetString());
        Assert.Equal("0/3", response.GetProperty("progress").GetString());
        Assert.Equal("get_test_case_details", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_MissingSuite_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_PARAMS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_EmptySuite_ReturnsInvalidSuiteError()
    {
        var tool = new StartExecutionRunTool(_engine, _ => Enumerable.Empty<TestIndexEntry>());
        var parameters = JsonDocument.Parse("""{"suite": "nonexistent"}""").RootElement;

        var result = await tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_SUITE", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ActiveRunExists_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout"}""").RootElement;

        // Start first run
        await _tool.ExecuteAsync(parameters);

        // Try to start second run
        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("ACTIVE_RUN_EXISTS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_WithEnvironment_IncludesInResponse()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "environment": "staging"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("run_id", out _));
    }

    [Fact]
    public async Task Execute_WithPriorityFilter_FiltersTests()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "filters": {"priority": "high"}}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task Execute_WithTagsFilter_FiltersTests()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "filters": {"tags": ["smoke"]}}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(2, data.GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task Execute_FilterMatchesNoTests_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "filters": {"tags": ["nonexistent"]}}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("NO_TESTS_MATCH", error.GetProperty("code").GetString());
    }

    // ---------------------------------------------------------------------
    // Spec 051 — US1: top-level plural filter shape (matches find_test_cases)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task StartExecutionRun_TopLevelPriorities_FiltersSuite()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "priorities": ["high"]}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var data = JsonDocument.Parse(result).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32()); // only TC-001 (high), not the full suite of 3
        Assert.Equal("TC-001", data.GetProperty("first_test").GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task StartExecutionRun_TopLevelMultiplePriorities_OrSemantics()
    {
        // "high" OR "critical" — critical is not a real priority so it matches nothing; union == the high tests.
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "priorities": ["high", "critical"]}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var data = JsonDocument.Parse(result).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
    }

    [Fact]
    public async Task StartExecutionRun_TopLevelMixedFilters_AndSemantics()
    {
        // priorities:[low] AND tags:[smoke] → only TC-003 (low + smoke). AND between fields.
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "priorities": ["low"], "tags": ["smoke"]}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var data = JsonDocument.Parse(result).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
        Assert.Equal("TC-003", data.GetProperty("first_test").GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task StartExecutionRun_LegacyNestedFilters_StillWorks()
    {
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "filters": {"priority": "high"}}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var data = JsonDocument.Parse(result).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
        Assert.Equal("TC-001", data.GetProperty("first_test").GetProperty("test_id").GetString());
    }

    [Fact]
    public async Task StartExecutionRun_BothShapes_TopLevelWins_LogsWarning()
    {
        // Top-level high wins over nested low → TC-001 (high), and a warning is surfaced.
        var parameters = JsonDocument.Parse(
            """{"suite": "checkout", "priorities": ["high"], "filters": {"priority": "low"}}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var data = JsonDocument.Parse(result).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetProperty("test_count").GetInt32());
        Assert.Equal("TC-001", data.GetProperty("first_test").GetProperty("test_id").GetString());
        Assert.True(data.TryGetProperty("warnings", out var warnings));
        Assert.NotEqual(JsonValueKind.Null, warnings.ValueKind);
        Assert.Contains("top-level", warnings[0].GetString(), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Spec 051 — US2: unmapped/misplaced fields produce an actionable error
    // ---------------------------------------------------------------------

    [Fact]
    public async Task StartExecutionRun_UnknownField_ReturnsActionableError()
    {
        // Top-level singular 'priority' (should be plural 'priorities').
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "priority": "high"}""").RootElement;

        var ex = await Assert.ThrowsAsync<McpInvalidParamsException>(() => _tool.ExecuteAsync(parameters));

        Assert.Contains("priority", ex.Message);
        Assert.Contains("priorities", ex.Message);
    }

    [Fact]
    public async Task StartExecutionRun_NestedPluralBranch_ReturnsActionableError()
    {
        // Plural 'priorities' nested inside the legacy 'filters' object — should be top-level.
        var parameters = JsonDocument.Parse("""{"suite": "checkout", "filters": {"priorities": ["high"]}}""").RootElement;

        var ex = await Assert.ThrowsAsync<McpInvalidParamsException>(() => _tool.ExecuteAsync(parameters));

        Assert.Contains("priorities", ex.Message);
        Assert.Contains("top-level", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
