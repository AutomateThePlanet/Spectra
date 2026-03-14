using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Integration;

public class ConcurrentUsersTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly McpConfig _config;
    private readonly List<TestIndexEntry> _testEntries;

    public ConcurrentUsersTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _db = new ExecutionDb(_testDir);
        _runRepo = new RunRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _config = new McpConfig { BasePath = _testDir, ReportsPath = Path.Combine(_testDir, "reports") };

        _testEntries =
        [
            new() { Id = "TC-001", File = "tc-001.md", Title = "Test One", Priority = "high" },
            new() { Id = "TC-002", File = "tc-002.md", Title = "Test Two", Priority = "medium" }
        ];
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task DifferentUsers_SameSuite_BothCanStart()
    {
        var identityA = new MockUserIdentityResolver("user-a");
        var identityB = new MockUserIdentityResolver("user-b");

        var engineA = new ExecutionEngine(_runRepo, _resultRepo, identityA, _config);
        var engineB = new ExecutionEngine(_runRepo, _resultRepo, identityB, _config);

        var startToolA = new StartExecutionRunTool(engineA, _ => _testEntries);
        var startToolB = new StartExecutionRunTool(engineB, _ => _testEntries);

        var resultA = await startToolA.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var resultB = await startToolB.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);

        var responseA = JsonDocument.Parse(resultA).RootElement;
        var responseB = JsonDocument.Parse(resultB).RootElement;

        Assert.True(responseA.TryGetProperty("data", out _));
        Assert.True(responseB.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task SameUser_SameSuite_SecondStartFails()
    {
        var identity = new MockUserIdentityResolver("user-a");
        var engine = new ExecutionEngine(_runRepo, _resultRepo, identity, _config);
        var startTool = new StartExecutionRunTool(engine, _ => _testEntries);

        var result1 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var result2 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);

        var response1 = JsonDocument.Parse(result1).RootElement;
        var response2 = JsonDocument.Parse(result2).RootElement;

        Assert.True(response1.TryGetProperty("data", out _));
        Assert.Equal("ACTIVE_RUN_EXISTS", response2.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SameUser_DifferentSuites_BothCanStart()
    {
        var identity = new MockUserIdentityResolver("user-a");
        var engine = new ExecutionEngine(_runRepo, _resultRepo, identity, _config);
        var startTool = new StartExecutionRunTool(engine, _ => _testEntries);

        var result1 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var result2 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "auth"}""").RootElement);

        var response1 = JsonDocument.Parse(result1).RootElement;
        var response2 = JsonDocument.Parse(result2).RootElement;

        Assert.True(response1.TryGetProperty("data", out _));
        Assert.True(response2.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task SameUser_AfterCancel_CanStartNew()
    {
        var identity = new MockUserIdentityResolver("user-a");
        var engine = new ExecutionEngine(_runRepo, _resultRepo, identity, _config);
        var startTool = new StartExecutionRunTool(engine, _ => _testEntries);
        var cancelTool = new CancelExecutionRunTool(engine);

        // Start first run
        var result1 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var runId = JsonDocument.Parse(result1).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        // Cancel it
        await cancelTool.ExecuteAsync(JsonDocument.Parse($$$"""{"run_id": "{{{runId}}}"}""").RootElement);

        // Start another
        var result2 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var response2 = JsonDocument.Parse(result2).RootElement;

        Assert.True(response2.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task SameUser_AfterComplete_CanStartNew()
    {
        var identity = new MockUserIdentityResolver("user-a");
        var engine = new ExecutionEngine(_runRepo, _resultRepo, identity, _config);
        var startTool = new StartExecutionRunTool(engine, _ => _testEntries);

        // Start first run
        var result1 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var runId = JsonDocument.Parse(result1).RootElement.GetProperty("data").GetProperty("run_id").GetString()!;

        // Finalize it (force)
        await engine.FinalizeRunAsync(runId, force: true);

        // Start another
        var result2 = await startTool.ExecuteAsync(JsonDocument.Parse("""{"suite": "checkout"}""").RootElement);
        var response2 = JsonDocument.Parse(result2).RootElement;

        Assert.True(response2.TryGetProperty("data", out _));
    }

    private sealed class MockUserIdentityResolver : IUserIdentityResolver
    {
        private readonly string _user;

        public MockUserIdentityResolver(string user)
        {
            _user = user;
        }

        public string GetCurrentUser() => _user;
    }
}
