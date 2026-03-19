using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Tools;

public class BulkRecordResultsTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly BulkRecordResultsTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public BulkRecordResultsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir };

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);
        _tool = new BulkRecordResultsTool(_engine);

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium" },
            new() { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low" },
            new() { Id = "TC-004", File = "tc-004.md", Title = "Test Four", Priority = "low" },
            new() { Id = "TC-005", File = "tc-005.md", Title = "Test Five", Priority = "low" }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_SkipsAllRemainingTests()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "SKIPPED", "remaining": true, "reason": "Environment not available"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(5, data.GetProperty("processed_count").GetInt32());
        Assert.Equal("SKIPPED", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_PassesAllRemainingTests()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "PASSED", "remaining": true}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(5, data.GetProperty("processed_count").GetInt32());
        Assert.Equal("PASSED", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_FailsAllRemainingTests()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "FAILED", "remaining": true, "reason": "Test environment crashed"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(5, data.GetProperty("processed_count").GetInt32());
        Assert.Equal("FAILED", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_BlocksAllRemainingTests()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "BLOCKED", "remaining": true, "reason": "Dependency failed"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(5, data.GetProperty("processed_count").GetInt32());
        Assert.Equal("BLOCKED", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_ProcessesSpecificTestIds()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "SKIPPED", "test_ids": ["TC-001", "TC-003"], "reason": "Selected tests skipped"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(2, data.GetProperty("processed_count").GetInt32());
    }

    [Fact]
    public async Task Execute_ReturnsNextTestIfAvailable()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "SKIPPED", "test_ids": ["TC-001", "TC-002"], "reason": "Skipping first two"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var data = response.GetProperty("data");
        var next = data.GetProperty("next");
        Assert.NotEqual(JsonValueKind.Null, next.ValueKind);
        Assert.True(next.TryGetProperty("test_handle", out _));
    }

    [Fact]
    public async Task Execute_MissingStatus_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""
            {"remaining": true}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_PARAMS", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_InvalidStatus_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""
            {"status": "INVALID", "remaining": true}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_STATUS", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MissingReasonForNonPassing_ReturnsError()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "SKIPPED", "remaining": true}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("REASON_REQUIRED", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_NoActiveRun_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""
            {"status": "PASSED", "remaining": true}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("NO_ACTIVE_RUN", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MissingBothRemainingAndTestIds_ReturnsError()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "PASSED"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_PARAMS", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ProcessedTestsHaveCorrectStatus()
    {
        await _engine.StartRunAsync("checkout", _testEntries);

        var parameters = JsonDocument.Parse("""
            {"status": "PASSED", "test_ids": ["TC-001"], "remaining": false}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var processedTests = response.GetProperty("data").GetProperty("processed_tests");
        var firstProcessed = processedTests.EnumerateArray().First();
        Assert.Equal("TC-001", firstProcessed.GetProperty("test_id").GetString());
        Assert.Equal("PASSED", firstProcessed.GetProperty("status").GetString());
    }
}
