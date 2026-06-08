using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests;

/// <summary>
/// Spec 065: the engine and its storage now live in the transport-neutral <c>Spectra.Execution</c>
/// assembly (namespaces preserved). This proves the relocation: the types resolve, they report the
/// new assembly, and a full start → advance → finalize round-trip works over a temp database with no
/// MCP transport involved.
/// </summary>
public class ExtractionSmokeTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly McpConfig _config;

    private readonly List<TestIndexEntry> _entries =
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Login", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Checkout", Priority = "medium" }
    ];

    public ExtractionSmokeTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-exec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _db = new ExecutionDb(_testDir);
        _config = new McpConfig { BasePath = _testDir };
    }

    private ExecutionEngine NewEngine() => new(
        new RunRepository(_db), new ResultRepository(_db), new QueueSnapshotRepository(_db),
        new UserIdentityResolver(), _config);

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void EngineAndStorage_LiveInSpectraExecutionAssembly()
    {
        Assert.Equal("Spectra.Execution", typeof(ExecutionEngine).Assembly.GetName().Name);
        Assert.Equal("Spectra.Execution", typeof(ExecutionDb).Assembly.GetName().Name);
        Assert.Equal("Spectra.Execution", typeof(UserIdentityResolver).Assembly.GetName().Name);
        Assert.Equal("Spectra.Execution", typeof(McpConfig).Assembly.GetName().Name);
    }

    [Fact]
    public async Task FullLoop_StartAdvanceFinalize_Works()
    {
        var (run, queue) = await NewEngine().StartRunAsync("suite", _entries);
        Assert.Equal(2, queue.TotalCount);

        // Advance both tests to terminal, then finalize.
        foreach (var t in queue.Tests.ToList())
        {
            await NewEngine().StartTestAsync(run.RunId, t.TestHandle);
            await NewEngine().AdvanceTestAsync(run.RunId, t.TestHandle, TestStatus.Passed);
        }

        var finalized = await NewEngine().FinalizeRunAsync(run.RunId);
        Assert.NotNull(finalized);
        Assert.Equal(RunStatus.Completed, finalized!.Status);
    }
}
