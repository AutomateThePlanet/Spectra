using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Tools;

public class SkipTestCaseTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly SkipTestCaseTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public SkipTestCaseTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir };

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);
        _tool = new SkipTestCaseTool(_engine, runRepo, resultRepo);

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", DependsOn = "TC-001" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low", DependsOn = "TC-002" }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_SkipsTestWithReason()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "reason": "Environment not available"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal("TC-001", data.GetProperty("skipped").GetProperty("test_id").GetString());
        Assert.Equal("Environment not available", data.GetProperty("skipped").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Execute_BlocksDependentTests()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "reason": "Skipping"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var blocked = response.GetProperty("data").GetProperty("blocked_tests");
        Assert.True(blocked.GetArrayLength() > 0);
        Assert.Contains("TC-002", blocked.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Execute_ReturnsNextTest()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "reason": "Skipping"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        // Next non-blocked test
        if (response.GetProperty("data").TryGetProperty("next", out var next) && next.ValueKind != JsonValueKind.Null)
        {
            Assert.True(next.TryGetProperty("test_handle", out _));
        }
    }

    [Fact]
    public async Task Execute_InvalidHandle_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""
            {"test_handle": "invalid", "reason": "Test"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_HANDLE", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MissingReason_ReturnsError()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_PARAMS", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_TestNotInProgress_ReturnsError()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "reason": "Test"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("TEST_NOT_IN_PROGRESS", response.GetProperty("error").GetProperty("code").GetString());
    }
}
