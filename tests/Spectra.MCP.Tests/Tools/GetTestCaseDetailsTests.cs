using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP.Tests.Tools;

public class GetTestCaseDetailsTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly ExecutionEngine _engine;
    private readonly GetTestCaseDetailsTool _tool;
    private readonly List<TestIndexEntry> _testEntries;

    public GetTestCaseDetailsTests()
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
            new TestIndexEntry { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium", Tags = ["regression"] }
        ];

        var testCase = new TestCase
        {
            Id = "TC-001",
            FilePath = "tc-001.md",
            Title = "Test One",
            Priority = Priority.High,
            Tags = ["smoke"],
            Component = "checkout",
            Preconditions = "User is logged in",
            Steps = ["Navigate to cart", "Click checkout", "Enter payment"],
            ExpectedResult = "Order is placed",
            TestData = "Card: 4111111111111111"
        };

        _tool = new GetTestCaseDetailsTool(_engine, (suite, testId) => testId == "TC-001" ? testCase : null);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private async Task<string> StartRunAndGetFirstHandle()
    {
        var (_, queue) = await _engine.StartRunAsync("checkout", _testEntries);
        return queue.GetNext()!.TestHandle;
    }

    [Fact]
    public async Task Execute_ValidHandle_ReturnsTestDetails()
    {
        var handle = await StartRunAndGetFirstHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal("TC-001", data.GetProperty("test_id").GetString());
        Assert.Equal("Test One", data.GetProperty("title").GetString());
        Assert.Equal("high", data.GetProperty("priority").GetString());
        Assert.Equal(3, data.GetProperty("step_count").GetInt32());
    }

    [Fact]
    public async Task Execute_ValidHandle_ReturnsSteps()
    {
        var handle = await StartRunAndGetFirstHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        var steps = response.GetProperty("data").GetProperty("steps");
        Assert.Equal(3, steps.GetArrayLength());
        Assert.Equal(1, steps[0].GetProperty("number").GetInt32());
        Assert.Equal("Navigate to cart", steps[0].GetProperty("action").GetString());
    }

    [Fact]
    public async Task Execute_ValidHandle_ReturnsRunningStatus()
    {
        var handle = await StartRunAndGetFirstHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("Running", response.GetProperty("run_status").GetString());
        Assert.Equal("advance_test_case", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_MissingHandle_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_PARAMS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_InvalidHandle_ReturnsError()
    {
        var parameters = JsonDocument.Parse("""{"test_handle": "invalid-handle"}""").RootElement;

        var result = await _tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("error", out var error));
        Assert.Equal("INVALID_HANDLE", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_MarksTestInProgress()
    {
        var handle = await StartRunAndGetFirstHandle();
        var parameters = JsonDocument.Parse($$$"""{"test_handle": "{{{handle}}}"}""").RootElement;

        await _tool.ExecuteAsync(parameters);

        var testResult = await _engine.GetTestResultAsync(handle);
        Assert.Equal(TestStatus.InProgress, testResult!.Status);
        Assert.NotNull(testResult.StartedAt);
    }
}
