using Spectra.CLI.Commands.Run;
using Spectra.CLI.Commands.Run.WebConsole;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run.WebConsole;

/// <summary>
/// Spec 066 (US1): the console write-back endpoint replicates the mechanical human-in-the-loop guardrails
/// of <c>RunHandler.AdvanceAsync</c> at the HTTP boundary (FR-005, SC-003) — re-asserting
/// <c>GuardrailTests.cs</c>. A rejected verdict records NOTHING (the target test stays Pending).
/// </summary>
public class ConsoleGuardrailTests
{
    private static async Task<RunServices> StartedRunAsync(RunTestWorkspace ws)
    {
        var services = new RunServices(ws.Root);
        await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());
        return services;
    }

    [Fact]
    public async Task Advance_WithoutStatus_Rejected_RecordsNothing()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = await StartedRunAsync(ws);

        var resp = await new ConsoleEndpoints(services).AdvanceAsync(null, null);

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("STATUS_REQUIRED", ((ConsoleError)resp.Body!).ErrorCode);
        Assert.Equal(TestStatus.Pending, await ReadStatus(ws.Root, "TC-001"));
    }

    [Fact]
    public async Task Advance_FailWithoutNotes_Rejected_RecordsNothing()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = await StartedRunAsync(ws);

        var resp = await new ConsoleEndpoints(services).AdvanceAsync("fail", "   ");

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("NOTES_REQUIRED", ((ConsoleError)resp.Body!).ErrorCode);
        Assert.Equal(TestStatus.Pending, await ReadStatus(ws.Root, "TC-001"));
    }

    [Fact]
    public async Task Advance_BlockedWithoutNotes_Rejected()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = await StartedRunAsync(ws);

        var resp = await new ConsoleEndpoints(services).AdvanceAsync("blocked", null);

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("NOTES_REQUIRED", ((ConsoleError)resp.Body!).ErrorCode);
        Assert.Equal(TestStatus.Pending, await ReadStatus(ws.Root, "TC-001"));
    }

    [Fact]
    public async Task Advance_InvalidStatus_Rejected()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = await StartedRunAsync(ws);

        var resp = await new ConsoleEndpoints(services).AdvanceAsync("maybe", "notes");

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("INVALID_STATUS", ((ConsoleError)resp.Body!).ErrorCode);
        Assert.Equal(TestStatus.Pending, await ReadStatus(ws.Root, "TC-001"));
    }

    [Fact]
    public async Task Note_WithoutText_Rejected()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = await StartedRunAsync(ws);

        var resp = await new ConsoleEndpoints(services).NoteAsync(null);

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("NOTE_REQUIRED", ((ConsoleError)resp.Body!).ErrorCode);
    }

    private static async Task<TestStatus> ReadStatus(string root, string testId)
    {
        await using var db = new ExecutionDb(Path.Combine(root, ".execution"));
        var runs = await new RunRepository(db).GetAllAsync(limit: 50);
        var resultRepo = new ResultRepository(db);
        foreach (var r in runs)
        {
            var row = (await resultRepo.GetByRunIdAsync(r.RunId)).FirstOrDefault(x => x.TestId == testId);
            if (row is not null) return row.Status;
        }
        throw new InvalidOperationException($"No result row for {testId}.");
    }
}
