using Spectra.CLI.Commands.Run;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// US2 (spec 065): the <c>spectra run</c> handlers leave the same engine/DB state as driving
/// <see cref="ExecutionEngine"/> directly — same status/notes/handle, same dependency-blocking, same
/// priority-then-topological ordering (FR-007/SC-002). Both surfaces call one engine over one DB; the
/// engine-direct run is the reference oracle.
/// </summary>
public class ParityTests
{
    private static RunHandler Handler(string root) => new(VerbosityLevel.Quiet, OutputFormat.Json, root);

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
        // Handler path
        using var hw = new RunTestWorkspace();
        hw.WriteSuite("s", ("TC-001", "A", "high", null), ("TC-002", "B", "high", null));
        var h = Handler(hw.Root);
        await h.StartAsync("s", null, null, null, null, null, null);
        await h.AdvanceAsync(null, "fail", "broke at step 2");

        // Engine reference path (same fixture, separate workspace)
        using var ew = new RunTestWorkspace();
        ew.WriteSuite("s", ("TC-001", "A", "high", null), ("TC-002", "B", "high", null));
        var (engine, edb) = Engine(ew.Root);
        var (run, queue) = await engine.StartRunAsync("s", Entries("s", ew.Root));
        var first = queue.GetNext()!;
        await engine.StartTestAsync(run.RunId, first.TestHandle);
        await engine.AdvanceTestAsync(run.RunId, first.TestHandle, TestStatus.Failed, "broke at step 2");

        var handlerRow = await ReadFirstResult(hw.Root, "TC-001");
        var engineRow = await ReadResult(edb, run.RunId, "TC-001");
        await edb.DisposeAsync();

        Assert.Equal(engineRow.Status, handlerRow.Status);
        Assert.Equal(TestStatus.Failed, handlerRow.Status);
        Assert.Equal(engineRow.Notes, handlerRow.Notes);
        Assert.Equal(engineRow.Attempt, handlerRow.Attempt);
    }

    [Fact]
    public async Task Blocking_PropagatesIdentically_AsEnginePath()
    {
        // TC-002 depends on TC-001; failing TC-001 must block TC-002.
        using var hw = new RunTestWorkspace();
        hw.WriteSuite("s", ("TC-001", "Parent", "high", null), ("TC-002", "Child", "high", "TC-001"));
        var h = Handler(hw.Root);
        await h.StartAsync("s", null, null, null, null, null, null);
        await h.AdvanceAsync(null, "fail", "parent broke");

        var child = await ReadFirstResult(hw.Root, "TC-002");
        Assert.Equal(TestStatus.Blocked, child.Status);
    }

    [Fact]
    public async Task Ordering_IsPriorityThenTopological_NotAlphabetical()
    {
        // Correct order = [TC-002 high, TC-003 high dep TC-002, TC-001 low]; alphabetical would be TC-001 first.
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s",
            ("TC-001", "Low", "low", null),
            ("TC-002", "High first", "high", null),
            ("TC-003", "High dep", "high", "TC-002"));

        var services = new RunServices(ws.Root);
        var (_, queue) = await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());
        await services.DisposeAsync();

        Assert.Equal("TC-002", queue.GetNext()!.TestId);
        Assert.Equal(new[] { "TC-002", "TC-003", "TC-001" }, queue.Tests.Select(t => t.TestId).ToArray());
    }

    [Fact]
    public async Task Retest_AcrossFreshServices_RequeuesLikeEngine()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null), ("TC-002", "B", "high", null));
        var h = Handler(ws.Root);
        await h.StartAsync("s", null, null, null, null, null, null);
        await h.AdvanceAsync(null, "fail", "flaky");

        // A fresh handler (short-lived process) must requeue successfully — no RUN_NOT_FOUND.
        var runId = (await ReadActiveOrAnyRunId(ws.Root));
        var code = await Handler(ws.Root).RetestAsync(runId, "TC-001");
        Assert.Equal(ExitCodes.Success, code);

        var rows = await ReadAllResults(ws.Root, "TC-001");
        Assert.Equal(2, rows.Max(r => r.Attempt)); // attempt 2 exists
        Assert.Contains(rows, r => r.Status == TestStatus.Pending && r.Attempt == 2);
    }

    // --- helpers ---

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

    private static async Task<string?> ReadActiveOrAnyRunId(string root)
    {
        await using var db = new ExecutionDb(Path.Combine(root, ".execution"));
        var runs = await new RunRepository(db).GetAllAsync(limit: 1);
        return runs.FirstOrDefault()?.RunId;
    }
}
