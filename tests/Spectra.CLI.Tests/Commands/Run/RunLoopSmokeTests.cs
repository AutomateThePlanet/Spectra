using Spectra.CLI.Commands.Run;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;
using Spectra.Core.Models.Execution;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// US1 (spec 065): a full execution loop runs purely through the <c>spectra run</c> handlers against
/// a workspace with only the CLI present and NO MCP config — start → advance(all) → finalize, with
/// durable, reportable results (SC-001).
/// </summary>
public class RunLoopSmokeTests
{
    private static RunHandler Handler(string root) =>
        new(VerbosityLevel.Quiet, OutputFormat.Json, root);

    [Fact]
    public async Task FullLoop_StartAdvanceFinalize_NoMcp()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("checkout",
            ("TC-001", "Login", "high", null),
            ("TC-002", "Checkout", "medium", null));

        Assert.False(ws.HasMcpConfig); // SC-001: no MCP config present

        var handler = Handler(ws.Root);

        Assert.Equal(ExitCodes.Success, await handler.StartAsync("checkout", null, null, null, null, null, null));

        // Advance both tests using auto-resolution of the in-progress/next handle (no handle passed).
        Assert.Equal(ExitCodes.Success, await handler.AdvanceAsync(null, "pass", null));
        Assert.Equal(ExitCodes.Success, await handler.AdvanceAsync(null, "pass", null));

        // Finalize and confirm the run completed + results are durable in the DB.
        Assert.Equal(ExitCodes.Success, await handler.FinalizeAsync(null, force: false));

        await using var db = new ExecutionDb(Path.Combine(ws.Root, ".execution"));
        var engine = new ExecutionEngine(new RunRepository(db), new ResultRepository(db),
            new QueueSnapshotRepository(db), new UserIdentityResolver(), new McpConfig { BasePath = ws.Root });
        var active = await engine.GetActiveRunAsync();
        Assert.Null(active); // finalized → no active run

        // The HTML report exists.
        var reportsDir = Path.Combine(ws.Root, ".execution", "reports");
        Assert.True(Directory.Exists(reportsDir));
        Assert.NotEmpty(Directory.GetFiles(reportsDir, "*.html"));
    }

    [Fact]
    public async Task Finalize_WithPendingTests_BlocksWithoutForce()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("checkout", ("TC-001", "Login", "high", null), ("TC-002", "Checkout", "medium", null));
        var handler = Handler(ws.Root);

        await handler.StartAsync("checkout", null, null, null, null, null, null);

        // Pending guard fires (non-zero) without --force.
        var code = await handler.FinalizeAsync(null, force: false);
        Assert.NotEqual(ExitCodes.Success, code);

        // --force completes it.
        Assert.Equal(ExitCodes.Success, await handler.FinalizeAsync(null, force: true));
    }
}
