using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Tools;

public class AddTestNoteTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly AddTestNoteTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public AddTestNoteTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir };

        _engine = new ExecutionEngine(runRepo, resultRepo, identity, config);
        _tool = new AddTestNoteTool(_engine, runRepo, resultRepo);

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high", Tags = ["smoke"] },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = ["regression"] }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_AddsNoteToTest()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "note": "UI shows spinner"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var data = response.GetProperty("data");
        Assert.Equal("TC-001", data.GetProperty("test_id").GetString());
        Assert.True(data.GetProperty("note_added").GetBoolean());
        Assert.Equal(1, data.GetProperty("total_notes").GetInt32());
    }

    [Fact]
    public async Task Execute_MultipleNotes_CountsCorrectly()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        // Add first note
        var params1 = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "note": "Note 1"}
            """).RootElement;
        await _tool.ExecuteAsync(params1);

        // Add second note
        var params2 = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "note": "Note 2"}
            """).RootElement;
        var result = await _tool.ExecuteAsync(params2);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(2, response.GetProperty("data").GetProperty("total_notes").GetInt32());
    }

    [Fact]
    public async Task Execute_InvalidHandle_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""
            {"test_handle": "invalid", "note": "Test note"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("INVALID_HANDLE", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MissingNote_ReturnsError()
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
    public async Task Execute_InProgressTest_ReturnsAdvanceAction()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "note": "Test note"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("advance_test_case", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_IncludesRunStatusAndProgress()
    {
        var (run, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        var firstTest = queue.Tests.First();
        await _engine.StartTestAsync(run.RunId, firstTest.TestHandle);

        var parameters = JsonDocument.Parse($$$"""
            {"test_handle": "{{{firstTest.TestHandle}}}", "note": "Test note"}
            """).RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Running", response.GetProperty("run_status").GetString());
        Assert.Equal("0/2", response.GetProperty("progress").GetString());
    }
}
