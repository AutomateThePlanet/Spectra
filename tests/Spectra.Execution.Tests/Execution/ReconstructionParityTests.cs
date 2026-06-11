using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Execution;

/// <summary>
/// US4 (spec 064): aggregate behavioural parity — a queue reconstructed in a fresh engine is
/// indistinguishable from the original in-memory queue across every observable field, including a
/// build → persist → advance → reconstruct round-trip.
/// </summary>
public class ReconstructionParityTests : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly ExecutionDb _db;
    private readonly McpConfig _config;

    private readonly List<TestIndexEntry> _entries =
    [
        new() { Id = "TC-001", File = "tc-001.md", Title = "Login", Priority = "high" },
        new() { Id = "TC-002", File = "tc-002.md", Title = "Checkout", Priority = "high", DependsOn = "TC-001" },
        new() { Id = "TC-003", File = "tc-003.md", Title = "Payment", Priority = "medium", DependsOn = "TC-002" },
        new() { Id = "TC-004", File = "tc-004.md", Title = "Logout", Priority = "low" }
    ];

    public ReconstructionParityTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
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

    private static void AssertQueuesEquivalent(TestQueue expected, TestQueue actual)
    {
        Assert.Equal(expected.TotalCount, actual.TotalCount);
        Assert.Equal(expected.PendingCount, actual.PendingCount);
        Assert.Equal(expected.CompletedCount, actual.CompletedCount);
        Assert.Equal(expected.GetNext()?.TestId, actual.GetNext()?.TestId);

        for (var i = 0; i < expected.TotalCount; i++)
        {
            var e = expected.Tests[i];
            var a = actual.Tests[i];
            Assert.Equal(e.TestId, a.TestId);       // same order
            Assert.Equal(e.Title, a.Title);
            Assert.Equal(e.Priority, a.Priority);
            Assert.Equal(e.DependsOn, a.DependsOn);
            Assert.Equal(e.Status, a.Status);
            Assert.Equal(e.TestHandle, a.TestHandle);
        }
    }

    [Fact]
    public async Task FreshQueue_IsEquivalentToOriginal_AtStart()
    {
        var (run, original) = await NewEngine().StartRunAsync("checkout", _entries);
        var reconstructed = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(reconstructed);
        AssertQueuesEquivalent(original, reconstructed!);
    }

    [Fact]
    public async Task RoundTrip_AfterAdvances_ReconstructionReflectsLatestState()
    {
        // Original process advances the first test, then a fresh process reconstructs.
        var origin = NewEngine();
        var (run, original) = await origin.StartRunAsync("checkout", _entries);
        var tc001 = original.Tests.First(t => t.TestId == "TC-001").TestHandle;

        await origin.StartTestAsync(run.RunId, tc001);
        await origin.AdvanceTestAsync(run.RunId, tc001, TestStatus.Passed);

        // The warm queue (origin) and a cold reconstruction must agree on everything.
        var warm = await origin.GetQueueAsync(run.RunId);
        var cold = await NewEngine().GetQueueAsync(run.RunId);

        Assert.NotNull(warm);
        Assert.NotNull(cold);
        AssertQueuesEquivalent(warm!, cold!);
        // TC-001 is Passed; next actionable test is its dependent TC-002.
        Assert.Equal(TestStatus.Passed, cold!.GetById("TC-001")!.Status);
        Assert.Equal("TC-002", cold.GetNext()?.TestId);
    }

    [Fact]
    public async Task Reconstruction_AfterRetest_UsesLatestAttemptHandleAndStatus()
    {
        var origin = NewEngine();
        var (run, original) = await origin.StartRunAsync("checkout", _entries);
        var tc001 = original.Tests.First(t => t.TestId == "TC-001").TestHandle;

        await origin.StartTestAsync(run.RunId, tc001);
        await origin.AdvanceTestAsync(run.RunId, tc001, TestStatus.Failed, "flaky");
        var retest = await origin.RetestAsync(run.RunId, "TC-001");
        Assert.NotNull(retest);

        var cold = await NewEngine().GetQueueAsync(run.RunId);
        Assert.NotNull(cold);
        var rebuiltTc001 = cold!.GetById("TC-001");
        Assert.NotNull(rebuiltTc001);
        // Latest attempt wins: new pending handle, Pending status — not the failed attempt's.
        Assert.Equal(retest!.TestHandle, rebuiltTc001!.TestHandle);
        Assert.Equal(TestStatus.Pending, rebuiltTc001.Status);
    }
}
