using System.Text.Json;
using Spectra.CLI.Commands.Run;
using Spectra.CLI.Commands.Run.WebConsole;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run.WebConsole;

/// <summary>
/// Spec 066 (US1/US2): the console write-back endpoint leaves the SAME DB state as driving
/// <see cref="ExecutionEngine"/> directly — mirroring <c>ParityTests.cs</c> at the console boundary
/// (FR-004/FR-013, SC-001). Also covers the lossless-refresh projection (US2/SC-002): a fresh
/// <see cref="ConsoleEndpoints"/> (new "process") re-projects identical state from SQLite.
/// </summary>
public class ConsoleParityTests
{
    private static (ExecutionEngine Engine, ExecutionDb Db) Engine(string root)
    {
        var db = new ExecutionDb(Path.Combine(root, ".execution"));
        var engine = new ExecutionEngine(new RunRepository(db), new ResultRepository(db),
            new QueueSnapshotRepository(db), new UserIdentityResolver(), new McpConfig { BasePath = root });
        return (engine, db);
    }

    private static List<TestIndexEntry> Entries(string suite, string root)
        => new RunServices(root).IndexLoader(suite).ToList();

    [Fact]
    public async Task Advance_LeavesSameRowState_AsEnginePath()
    {
        // Console path
        using var cw = new RunTestWorkspace();
        cw.WriteSuite("s", ("TC-001", "A", "high", null), ("TC-002", "B", "high", null));
        await using (var services = new RunServices(cw.Root))
        {
            await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());
            var resp = await new ConsoleEndpoints(services).AdvanceAsync("fail", "broke at step 2");
            Assert.Equal(200, resp.StatusCode);
        }

        // Engine reference path (same fixture, separate workspace)
        using var ew = new RunTestWorkspace();
        ew.WriteSuite("s", ("TC-001", "A", "high", null), ("TC-002", "B", "high", null));
        var (engine, edb) = Engine(ew.Root);
        var (run, queue) = await engine.StartRunAsync("s", Entries("s", ew.Root));
        var first = queue.GetNext()!;
        await engine.StartTestAsync(run.RunId, first.TestHandle);
        await engine.AdvanceTestAsync(run.RunId, first.TestHandle, TestStatus.Failed, "broke at step 2");

        var consoleRow = await ReadFirstResult(cw.Root, "TC-001");
        var engineRow = await ReadResult(edb, run.RunId, "TC-001");
        await edb.DisposeAsync();

        Assert.Equal(TestStatus.Failed, consoleRow.Status);
        Assert.Equal(engineRow.Status, consoleRow.Status);
        Assert.Equal(engineRow.Notes, consoleRow.Notes);
        Assert.Equal(engineRow.Attempt, consoleRow.Attempt);
    }

    [Fact]
    public async Task Blocking_PropagatesIdentically_AsEnginePath()
    {
        using var cw = new RunTestWorkspace();
        cw.WriteSuite("s", ("TC-001", "Parent", "high", null), ("TC-002", "Child", "high", "TC-001"));
        await using (var services = new RunServices(cw.Root))
        {
            await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());
            await new ConsoleEndpoints(services).AdvanceAsync("fail", "parent broke");
        }

        var child = await ReadFirstResult(cw.Root, "TC-002");
        Assert.Equal(TestStatus.Blocked, child.Status);
    }

    [Fact]
    public async Task FreshProjection_ReflectsRecordedState_AfterRefresh()
    {
        // US2/SC-002: a refresh (new ConsoleEndpoints over the same DB) loses nothing — TC-001 PASSED,
        // current advances to TC-002 — because the browser holds no state; SQLite is the source of truth.
        using var cw = new RunTestWorkspace();
        cw.WriteSuite("s", ("TC-001", "A", "high", null), ("TC-002", "B", "high", null));
        await using (var services = new RunServices(cw.Root))
        {
            await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());
            await new ConsoleEndpoints(services).AdvanceAsync("pass", null);
        }

        await using var fresh = new RunServices(cw.Root);
        var resp = await new ConsoleEndpoints(fresh).GetCurrentAsync();
        Assert.Equal(200, resp.StatusCode);
        var json = JsonSerializer.Serialize(resp.Body);
        Assert.Contains("TC-002", json);   // current advanced to the next test
        Assert.Contains("PASSED", json);    // TC-001 recorded in the results projection
    }

    [Fact]
    public void Page_HoldsNoBrowserState()
    {
        // FR-002/FR-003: the page never stores authoritative run state in the browser.
        var html = ConsolePage.Render();
        Assert.DoesNotContain("localStorage", html);
        Assert.DoesNotContain("sessionStorage", html);
    }

    // --- helpers (mirror ParityTests.cs) ---

    private static async Task<TestResult> ReadFirstResult(string root, string testId)
    {
        var rows = await ReadAllResults(root, testId);
        return rows.OrderByDescending(r => r.Attempt).First();
    }

    private static async Task<List<TestResult>> ReadAllResults(string root, string testId)
    {
        await using var db = new ExecutionDb(Path.Combine(root, ".execution"));
        var runRepo = new RunRepository(db);
        var resultRepo = new ResultRepository(db);
        var runs = await runRepo.GetAllAsync(limit: 50);
        var all = new List<TestResult>();
        foreach (var r in runs)
            all.AddRange((await resultRepo.GetByRunIdAsync(r.RunId)).Where(x => x.TestId == testId));
        return all;
    }

    private static async Task<TestResult> ReadResult(ExecutionDb db, string runId, string testId)
    {
        var resultRepo = new ResultRepository(db);
        return (await resultRepo.GetByRunIdAsync(runId)).First(x => x.TestId == testId);
    }
}
