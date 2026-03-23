using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class GetExecutionStatusTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;
    private readonly List<TestIndexEntry> _testEntries;
    private readonly Dictionary<string, TestCase> _testCases;

    public GetExecutionStatusTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = _testDir };

        _engine = new ExecutionEngine(_runRepo, resultRepo, identity, config);

        _testEntries =
        [
            new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "Login Test", Priority = "high", Tags = ["smoke"] },
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Checkout Test", Priority = "medium", Tags = [] }
        ];

        _testCases = new Dictionary<string, TestCase>
        {
            ["TC-001"] = new()
            {
                Id = "TC-001", FilePath = "tc-001.md", Title = "Login Test",
                Priority = Priority.High, Tags = ["smoke"],
                Steps = ["Open login page", "Enter credentials", "Click login button"],
                ExpectedResult = "User is logged in successfully",
                Preconditions = "User has valid credentials"
            },
            ["TC-002"] = new()
            {
                Id = "TC-002", FilePath = "tc-002.md", Title = "Checkout Test",
                Priority = Priority.Medium, Tags = [],
                Steps = ["Add item to cart", "Go to checkout"],
                ExpectedResult = "Order is placed"
            }
        };
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
    public async Task Execute_ReturnsInstructionField()
    {
        var tool = new GetExecutionStatusTool(_engine, _runRepo, (_, id) => _testCases.GetValueOrDefault(id));
        await _engine.StartRunAsync("checkout", _testEntries);

        var result = await tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("instruction", out var instruction));
        var text = instruction.GetString()!;
        Assert.Contains("NEXT STEP", text);
        Assert.Contains("advance_test_case", text);
        Assert.Contains("TC-001", text);
    }

    [Fact]
    public async Task Execute_WithTestLoader_ReturnsInlineTestDetails()
    {
        var tool = new GetExecutionStatusTool(_engine, _runRepo, (_, id) => _testCases.GetValueOrDefault(id));
        await _engine.StartRunAsync("checkout", _testEntries);

        var result = await tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        var currentTest = response.GetProperty("data").GetProperty("current_test");
        Assert.Equal("TC-001", currentTest.GetProperty("test_id").GetString());
        Assert.Equal("Login Test", currentTest.GetProperty("title").GetString());

        // Inline test details should be present
        var details = currentTest.GetProperty("details");
        Assert.Equal(3, details.GetProperty("step_count").GetInt32());
        Assert.Equal("User is logged in successfully", details.GetProperty("expected_result").GetString());
        Assert.Equal("User has valid credentials", details.GetProperty("preconditions").GetString());

        var steps = details.GetProperty("steps");
        Assert.Equal(3, steps.GetArrayLength());
        Assert.Equal(1, steps[0].GetProperty("number").GetInt32());
        Assert.Equal("Open login page", steps[0].GetProperty("action").GetString());
    }

    [Fact]
    public async Task Execute_WithoutTestLoader_ReturnsNullDetails()
    {
        var tool = new GetExecutionStatusTool(_engine, _runRepo);
        await _engine.StartRunAsync("checkout", _testEntries);

        var result = await tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        var currentTest = response.GetProperty("data").GetProperty("current_test");
        Assert.Equal("TC-001", currentTest.GetProperty("test_id").GetString());
        // details should be null when no test loader
        Assert.Equal(JsonValueKind.Null, currentTest.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task Execute_AllTestsDone_InstructionSaysFinalizeRun()
    {
        var tool = new GetExecutionStatusTool(_engine, _runRepo, (_, id) => _testCases.GetValueOrDefault(id));
        var entries = new List<TestIndexEntry>
        {
            new() { Id = "TC-001", File = "tc-001.md", Title = "Only Test", Priority = "high", Tags = [] }
        };
        var (run, queue) = await _engine.StartRunAsync("single", entries);
        var handle = queue.GetNext()!.TestHandle;
        await _engine.StartTestAsync(run.RunId, handle);
        await _engine.AdvanceTestAsync(run.RunId, handle, TestStatus.Passed);

        var result = await tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        var instruction = response.GetProperty("instruction").GetString()!;
        Assert.Contains("finalize_execution_run", instruction);
    }

    [Fact]
    public async Task Execute_PausedRun_InstructionSaysResume()
    {
        var tool = new GetExecutionStatusTool(_engine, _runRepo);
        var (run, _) = await _engine.StartRunAsync("checkout", _testEntries);
        await _engine.PauseRunAsync(run.RunId);

        var result = await tool.ExecuteAsync(null);
        var response = JsonDocument.Parse(result).RootElement;

        var instruction = response.GetProperty("instruction").GetString()!;
        Assert.Contains("resume_execution_run", instruction);
    }
}
