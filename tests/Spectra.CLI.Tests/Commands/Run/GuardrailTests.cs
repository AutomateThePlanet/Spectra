using Spectra.CLI.Commands.Run;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// US5 (spec 065): the CLI surface enforces human-in-the-loop guardrails mechanically — it never
/// advances without an explicit verdict and only records exactly the verdict supplied, never an
/// inferred one (SC-006).
/// </summary>
public class GuardrailTests
{
    private static RunHandler Handler(string root) => new(VerbosityLevel.Quiet, OutputFormat.Json, root);

    private static async Task<TestStatus> ReadStatus(string root, string testId)
    {
        await using var db = new ExecutionDb(Path.Combine(root, ".execution"));
        var runs = await new RunRepository(db).GetAllAsync(limit: 1);
        var rows = await new ResultRepository(db).GetByRunIdAsync(runs[0].RunId);
        return rows.First(r => r.TestId == testId).Status;
    }

    [Fact]
    public async Task Advance_WithoutStatus_DoesNotRecordOrAdvance()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        var h = Handler(ws.Root);
        await h.StartAsync("s", null, null, null, null, null, null);

        // No --status → must NOT record and must return a non-success (missing-argument) code.
        var code = await h.AdvanceAsync(handle: null, status: null, notes: null);
        Assert.NotEqual(ExitCodes.Success, code);

        // The test is still Pending — nothing was recorded.
        Assert.Equal(TestStatus.Pending, await ReadStatus(ws.Root, "TC-001"));
    }

    [Fact]
    public async Task Advance_RecordsExactlyTheSuppliedVerdict()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        var h = Handler(ws.Root);
        await h.StartAsync("s", null, null, null, null, null, null);

        await h.AdvanceAsync(null, "fail", "explicit failure note");
        Assert.Equal(TestStatus.Failed, await ReadStatus(ws.Root, "TC-001")); // not inferred Passed
    }

    [Fact]
    public async Task Advance_FailWithoutNotes_IsRejected()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        var h = Handler(ws.Root);
        await h.StartAsync("s", null, null, null, null, null, null);

        var code = await h.AdvanceAsync(null, "fail", notes: null);
        Assert.NotEqual(ExitCodes.Success, code);
        Assert.Equal(TestStatus.Pending, await ReadStatus(ws.Root, "TC-001"));
    }
}
