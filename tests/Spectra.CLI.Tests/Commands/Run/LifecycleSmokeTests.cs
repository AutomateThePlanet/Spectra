using Spectra.CLI.Commands.Run;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// Spec 070 (US1 / SC-001) — the full execution lifecycle runs through the <c>spectra run</c> handlers on
/// a workspace with NO MCP config present: start → advance → pause → resume → bulk-record(remaining) →
/// finalize, with a durable report. Complements <c>RunLoopSmokeTests</c> (start/advance/finalize) by
/// covering the pause/resume and bulk-record legs the MCP tools used to own. Screenshot capture is covered
/// by <c>WebConsole/ConsoleScreenshotTests</c>.
/// </summary>
public class LifecycleSmokeTests
{
    private static RunHandler Handler(string root) => new(VerbosityLevel.Quiet, OutputFormat.Json, root);

    [Fact]
    public async Task FullLifecycle_PauseResumeBulkRecordFinalize_NoMcp()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("checkout",
            ("TC-001", "Login", "high", null),
            ("TC-002", "Cart", "medium", null),
            ("TC-003", "Pay", "low", null));

        Assert.False(ws.HasMcpConfig); // SC-001: no MCP config present

        var h = Handler(ws.Root);

        Assert.Equal(ExitCodes.Success, await h.StartAsync("checkout", null, null, null, null, null, null));

        // Advance the first test with an explicit verdict.
        Assert.Equal(ExitCodes.Success, await h.AdvanceAsync(null, "pass", null));

        // Pause then resume across the same durable SQLite state.
        Assert.Equal(ExitCodes.Success, await h.PauseAsync(null));
        Assert.Equal(ExitCodes.Success, await h.ResumeAsync(null));

        // Bulk-record every remaining test in one operation (the retired bulk_record_results successor).
        Assert.Equal(ExitCodes.Success, await h.BulkRecordAsync("skip", remaining: true, testIds: null, reason: "env down", runId: null));

        // Finalize completes the run and writes a durable HTML report — with no MCP server at any point.
        Assert.Equal(ExitCodes.Success, await h.FinalizeAsync(null, force: false));

        await using var db = new ExecutionDb(Path.Combine(ws.Root, ".execution"));
        var engine = new ExecutionEngine(new RunRepository(db), new ResultRepository(db),
            new QueueSnapshotRepository(db), new UserIdentityResolver(), new McpConfig { BasePath = ws.Root });
        Assert.Null(await engine.GetActiveRunAsync()); // finalized → no active run

        var reportsDir = Path.Combine(ws.Root, ".execution", "reports");
        Assert.True(Directory.Exists(reportsDir));
        Assert.NotEmpty(Directory.GetFiles(reportsDir, "*.html"));
    }
}
