using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
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

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);

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
}
