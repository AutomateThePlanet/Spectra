using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Tools;

public class AdvanceTestCaseTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly AdvanceTestCaseTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public AdvanceTestCaseTests()
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
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = [] },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = [], DependsOn = "TC-001" },
            new TestIndexEntry { Id = "TC-003", File = "tc-003.md", Title = "Test Three", Priority = "low", Tags = [] }
        ];

        _tool = new AdvanceTestCaseTool(_engine);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private async Task<string> StartRunAndGetInProgressHandle()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var handle = queue.GetNext()!.TestHandle;
        await _engine.StartTestAsync(run.RunId, handle);
        return handle;
    }

    [Fact]
    public async Task Execute_PassedStatus_RecordsAndReturnsNext()
    {
        var handle = await StartRunAndGetInProgressHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal("TC-001", data.GetProperty("recorded").GetProperty("test_id").GetString());
        Assert.Equal("PASSED", data.GetProperty("recorded").GetProperty("status").GetString());
        Assert.True(data.TryGetProperty("next", out var next));
        Assert.NotNull(next.GetProperty("test_handle").GetString());
    }

    [Fact]
    public async Task Execute_FailedStatus_RecordsAndBlocksDependents()
    {
        var handle = await StartRunAndGetInProgressHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "FAILED"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var data = response.GetProperty("data");
        Assert.Equal("FAILED", data.GetProperty("recorded").GetProperty("status").GetString());

        var blocked = data.GetProperty("blocked_tests");
        Assert.Equal(1, blocked.GetArrayLength());
        Assert.Equal("TC-002", blocked[0].GetString());
    }

    [Fact]
    public async Task Execute_WithNotes_SavesNotes()
    {
        var handle = await StartRunAndGetInProgressHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED", "notes": "Worked well"}""").RootElement;

        await _tool.ExecuteAsync(parameters);

        var testResult = await _engine.GetTestResultAsync(handle);
        Assert.Equal("Worked well", testResult!.Notes);
    }

    [Fact]
    public async Task Execute_ReturnsCorrectProgress()
    {
        var handle = await StartRunAndGetInProgressHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("1/3", response.GetProperty("progress").GetString());
    }

    [Fact]
    public async Task Execute_LastTest_ReturnsNullNext()
    {
        var (run, queue) = await _engine.StartRunAsync("single", [
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Only Test", Priority = "high", Tags = [] }
        ]);
        var handle = queue.GetNext()!.TestHandle;
        await _engine.StartTestAsync(run.RunId, handle);

        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED"}""").RootElement;
        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(JsonValueKind.Null, response.GetProperty("data").GetProperty("next").ValueKind);
        Assert.Equal("finalize_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_InvalidStatus_ReturnsError()
    {
        var handle = await StartRunAndGetInProgressHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "INVALID"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_STATUS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_TestNotInProgress_ReturnsError()
    {
        var (_, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var handle = queue.GetNext()!.TestHandle;
        // Don't start the test (leave it pending)

        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}", "status": "PASSED"}""").RootElement;
        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("TEST_NOT_IN_PROGRESS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_InvalidHandle_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{"test_handle": "invalid", "status": "PASSED"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_HANDLE", error.GetProperty("code").GetString());
    }
}
