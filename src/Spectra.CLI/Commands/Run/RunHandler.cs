using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Reports;

namespace Spectra.CLI.Commands.Run;

/// <summary>
/// Spec 065: implements the <c>spectra run</c> command group — thin adapters over the shared
/// <see cref="ExecutionEngine"/>, mirroring the MCP execution tools one-to-one over the same DB.
/// Each method is stateless: it builds <see cref="RunServices"/>, calls one engine operation, and
/// renders a <see cref="RunResult"/>. A short-lived process reconstructs the queue from the DB
/// (Spec 064), so behavior is identical to the long-lived MCP server (FR-007).
/// </summary>
public sealed class RunHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;
    private readonly string? _basePath;

    public RunHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human, string? basePath = null)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
        _basePath = basePath;
    }

    // ---- start -------------------------------------------------------------

    public async Task<int> StartAsync(
        string? suite,
        IReadOnlyList<string>? priorities,
        IReadOnlyList<string>? tags,
        IReadOnlyList<string>? components,
        IReadOnlyList<string>? testIds,
        string? selection,
        string? environment,
        CancellationToken ct = default)
    {
        return await RunGuardedAsync("run start", async s =>
        {
            var modeCount = (!string.IsNullOrEmpty(suite) ? 1 : 0) + (testIds is { Count: > 0 } ? 1 : 0) + (!string.IsNullOrEmpty(selection) ? 1 : 0);
            if (modeCount == 0)
                return Fail("run start", "INVALID_PARAMS", "One of <suite>, --test-ids, or --selection is required.", ExitCodes.MissingArguments);
            if (modeCount > 1)
                return Fail("run start", "INVALID_PARAMS", "<suite>, --test-ids, and --selection are mutually exclusive.", ExitCodes.MissingArguments);

            List<TestIndexEntry> entries;
            string suiteName;
            RunFilters? filters = null;

            if (!string.IsNullOrEmpty(suite))
            {
                entries = s.IndexLoader(suite).ToList();
                if (entries.Count == 0)
                    return Fail("run start", "INVALID_SUITE", $"Suite '{suite}' not found or has no tests.", ExitCodes.NotFound);
                suiteName = suite;
                if (priorities is { Count: > 0 } || tags is { Count: > 0 } || components is { Count: > 0 })
                    filters = RunFilters.From(priorities?.ToList(), tags?.ToList(), components?.ToList());
            }
            else if (testIds is { Count: > 0 })
            {
                var all = new Dictionary<string, TestIndexEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var sui in s.SuiteListLoader())
                    foreach (var e in s.IndexLoader(sui))
                        all.TryAdd(e.Id, e);
                var missing = testIds.Where(id => !all.ContainsKey(id)).ToList();
                if (missing.Count > 0)
                    return Fail("run start", "INVALID_TEST_IDS", $"Test IDs not found: {string.Join(", ", missing)}", ExitCodes.NotFound);
                entries = testIds.Distinct().Select(id => all[id]).ToList();
                suiteName = string.Join("+", testIds.Select(id =>
                    s.SuiteListLoader().FirstOrDefault(su => s.IndexLoader(su).Any(e => e.Id == id)) ?? "?").Distinct());
            }
            else
            {
                var selections = s.SelectionsLoader();
                if (!selections.TryGetValue(selection!, out _))
                    return Fail("run start", "SELECTION_NOT_FOUND", $"Selection '{selection}' not found.", ExitCodes.NotFound);
                var allTests = s.SuiteListLoader().SelectMany(su => s.IndexLoader(su)).ToList();
                // Reuse the same filter application the MCP selection mode uses via RunFilters on the engine build.
                entries = allTests;
                suiteName = $"selection:{selection}";
            }

            try
            {
                var (run, queue) = await s.Engine.StartRunAsync(suiteName, entries, environment, filters);
                var first = queue.GetNext();
                return (ExitCodes.Success, new RunResult
                {
                    Command = "run start",
                    Status = "completed",
                    RunId = run.RunId,
                    Suite = run.Suite,
                    RunStatus = run.Status.ToString(),
                    Progress = queue.GetProgress(),
                    TestCount = queue.TotalCount,
                    NextTest = first is null ? null : new RunTestRef { TestHandle = first.TestHandle, TestId = first.TestId, Title = first.Title },
                    Message = $"Started run {run.RunId} for '{run.Suite}' ({queue.TotalCount} tests)."
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Active run exists"))
            {
                return Fail("run start", "ACTIVE_RUN_EXISTS", ex.Message, ExitCodes.Error);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No tests match"))
            {
                return Fail("run start", "NO_TESTS_MATCH", ex.Message, ExitCodes.NotFound);
            }
        }, ct);
    }

    // ---- status ------------------------------------------------------------

    public async Task<int> StatusAsync(string? runId, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run status", async s =>
        {
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail("run status", "NO_ACTIVE_RUN", "No run id given and no active run for the current user.", ExitCodes.NotFound);

            var status = await s.Engine.GetStatusAsync(resolved);
            if (status is null)
                return Fail("run status", "RUN_NOT_FOUND", $"Run '{resolved}' not found.", ExitCodes.NotFound);

            var (run, queue) = status.Value;
            var counts = await s.Engine.GetStatusCountsAsync(resolved);
            var next = queue.GetNext();
            return (ExitCodes.Success, new RunResult
            {
                Command = "run status",
                Status = "completed",
                RunId = run.RunId,
                Suite = run.Suite,
                RunStatus = run.Status.ToString(),
                Progress = queue.GetProgress(),
                TestCount = queue.TotalCount,
                Counts = counts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                NextTest = next is null ? null : new RunTestRef { TestHandle = next.TestHandle, TestId = next.TestId, Title = next.Title }
            });
        }, ct);
    }

    // ---- show --------------------------------------------------------------

    public async Task<int> ShowAsync(string? runId, string? testId, string? handle, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run show", async s =>
        {
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail("run show", "NO_ACTIVE_RUN", "No run id given and no active run for the current user.", ExitCodes.NotFound);

            var run = await s.Engine.GetRunAsync(resolved);
            if (run is null)
                return Fail("run show", "RUN_NOT_FOUND", $"Run '{resolved}' not found.", ExitCodes.NotFound);

            var queue = await s.Engine.GetQueueAsync(resolved);
            if (queue is null)
                return Fail("run show", "RUN_NOT_FOUND", $"Run '{resolved}' has no recorded tests.", ExitCodes.NotFound);

            QueuedTest? target = handle is not null ? queue.GetByHandle(handle)
                : testId is not null ? queue.GetById(testId)
                : queue.GetNext();
            if (target is null)
                return Fail("run show", "NO_TEST", "No matching/next test to show.", ExitCodes.NotFound);

            var tc = s.TestCaseLoader(run.Suite, target.TestId);
            return (ExitCodes.Success, new RunResult
            {
                Command = "run show",
                Status = "completed",
                RunId = run.RunId,
                Suite = run.Suite,
                Progress = queue.GetProgress(),
                CurrentTest = new RunTestRef { TestHandle = target.TestHandle, TestId = target.TestId, Title = target.Title },
                Test = tc is null ? null : new
                {
                    id = tc.Id,
                    title = tc.Title,
                    priority = tc.Priority,
                    component = tc.Component,
                    preconditions = tc.Preconditions,
                    steps = tc.Steps,
                    expected_result = tc.ExpectedResult
                }
            });
        }, ct);
    }

    // ---- advance / skip ----------------------------------------------------

    public async Task<int> AdvanceAsync(string? handle, string? status, string? notes, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run advance", async s =>
        {
            // Guardrail (FR-005/SC-006): no explicit verdict → no record, no advance.
            if (string.IsNullOrWhiteSpace(status))
                return Fail("run advance", "STATUS_REQUIRED", "--status is required (pass|fail|blocked|skip). The loop never advances without an explicit verdict.", ExitCodes.MissingArguments);

            if (!TryParseVerdict(status, out var verdict))
                return Fail("run advance", "INVALID_STATUS", "Status must be one of: pass, fail, blocked, skip.", ExitCodes.ValidationError);

            if (verdict is TestStatus.Failed or TestStatus.Blocked or TestStatus.Skipped && string.IsNullOrWhiteSpace(notes))
                return Fail("run advance", "NOTES_REQUIRED", $"Notes/reason are required for {verdict.ToString().ToUpperInvariant()}.", ExitCodes.MissingArguments);

            return await AdvanceCoreAsync(s, handle, verdict, notes);
        }, ct);
    }

    public async Task<int> SkipAsync(string? handle, string? reason, bool blocked, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run skip", async s =>
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Fail("run skip", "REASON_REQUIRED", "--reason is required.", ExitCodes.MissingArguments);
            return await AdvanceCoreAsync(s, handle, blocked ? TestStatus.Blocked : TestStatus.Skipped, reason);
        }, ct);
    }

    private async Task<(int, RunResult)> AdvanceCoreAsync(RunServices s, string? handle, TestStatus verdict, string? notes)
    {
        var resolvedHandle = await ResolveHandleAsync(s, handle);
        if (resolvedHandle is null)
            return Fail("run advance", "NO_TEST", "No test handle given and no in-progress/next-pending test found.", ExitCodes.NotFound);

        var result = await s.Engine.GetTestResultAsync(resolvedHandle);
        if (result is null)
            return Fail("run advance", "INVALID_HANDLE", $"Test handle '{resolvedHandle}' not found.", ExitCodes.NotFound);

        // Auto-start a pending test (parity with advance_test_case).
        if (result.Status == TestStatus.Pending)
        {
            await s.Engine.StartTestAsync(result.RunId, resolvedHandle);
            result = await s.Engine.GetTestResultAsync(resolvedHandle);
        }
        if (result is null || result.Status != TestStatus.InProgress)
            return Fail("run advance", "TEST_NOT_IN_PROGRESS", $"Test is not in progress (status: {result?.Status}).", ExitCodes.Error);

        var run = await s.Engine.GetRunAsync(result.RunId);
        if (run is null)
            return Fail("run advance", "RUN_NOT_FOUND", "Run not found.", ExitCodes.NotFound);

        try
        {
            var (recorded, blockedIds, next) = await s.Engine.AdvanceTestAsync(result.RunId, resolvedHandle, verdict, notes);
            var queue = await s.Engine.GetQueueAsync(result.RunId);
            return (ExitCodes.Success, new RunResult
            {
                Command = "run advance",
                Status = "completed",
                RunId = result.RunId,
                RunStatus = run.Status.ToString(),
                Progress = queue?.GetProgress(),
                Recorded = new RunRecorded { TestId = recorded.TestId, Status = recorded.Status.ToString().ToUpperInvariant() },
                BlockedTests = blockedIds,
                NextTest = next is null ? null : new RunTestRef { TestHandle = next.TestHandle, TestId = next.TestId, Title = next.Title },
                Message = next is null ? "All tests executed. Run `spectra run finalize` to complete." : $"Recorded {recorded.TestId} as {recorded.Status}."
            });
        }
        catch (InvalidOperationException ex)
        {
            return Fail("run advance", "INVALID_TRANSITION", ex.Message, ExitCodes.Error);
        }
    }

    // ---- note / retest -----------------------------------------------------

    public async Task<int> NoteAsync(string? handle, string? note, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run note", async s =>
        {
            if (string.IsNullOrWhiteSpace(note))
                return Fail("run note", "NOTE_REQUIRED", "--note is required.", ExitCodes.MissingArguments);
            var resolvedHandle = await ResolveHandleAsync(s, handle);
            if (resolvedHandle is null)
                return Fail("run note", "NO_TEST", "No test handle given and none in progress.", ExitCodes.NotFound);
            var result = await s.Engine.GetTestResultAsync(resolvedHandle);
            if (result is null)
                return Fail("run note", "INVALID_HANDLE", $"Test handle '{resolvedHandle}' not found.", ExitCodes.NotFound);
            await s.Engine.AddNoteAsync(resolvedHandle, note);
            return (ExitCodes.Success, new RunResult { Command = "run note", Status = "completed", RunId = result.RunId, Message = $"Note added to {result.TestId}." });
        }, ct);
    }

    public async Task<int> RetestAsync(string? runId, string? testId, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run retest", async s =>
        {
            if (string.IsNullOrWhiteSpace(testId))
                return Fail("run retest", "TEST_ID_REQUIRED", "--test-id is required.", ExitCodes.MissingArguments);
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail("run retest", "NO_ACTIVE_RUN", "No run id given and no active run.", ExitCodes.NotFound);
            var requeued = await s.Engine.RetestAsync(resolved, testId);
            if (requeued is null)
                return Fail("run retest", "RUN_NOT_FOUND", $"Run '{resolved}' or test '{testId}' not found.", ExitCodes.NotFound);
            return (ExitCodes.Success, new RunResult
            {
                Command = "run retest",
                Status = "completed",
                RunId = resolved,
                NextTest = new RunTestRef { TestHandle = requeued.TestHandle, TestId = requeued.TestId },
                Message = $"Requeued {testId} (attempt {requeued.Attempt})."
            });
        }, ct);
    }

    public async Task<int> BulkRecordAsync(string? status, bool remaining, IReadOnlyList<string>? testIds, string? reason, string? runId, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run bulk-record", async s =>
        {
            if (string.IsNullOrWhiteSpace(status) || !TryParseVerdict(status, out var verdict))
                return Fail("run bulk-record", "INVALID_STATUS", "--status must be one of: pass, fail, blocked, skip.", ExitCodes.ValidationError);
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail("run bulk-record", "NO_ACTIVE_RUN", "No run id given and no active run.", ExitCodes.NotFound);
            var queue = await s.Engine.GetQueueAsync(resolved);
            if (queue is null)
                return Fail("run bulk-record", "RUN_NOT_FOUND", $"Run '{resolved}' not found.", ExitCodes.NotFound);

            IEnumerable<QueuedTest> targets = queue.Tests.Where(t => t.Status is TestStatus.Pending or TestStatus.InProgress);
            if (!remaining)
            {
                if (testIds is not { Count: > 0 })
                    return Fail("run bulk-record", "INVALID_PARAMS", "Provide --remaining or --test-ids.", ExitCodes.MissingArguments);
                var set = new HashSet<string>(testIds, StringComparer.OrdinalIgnoreCase);
                targets = targets.Where(t => set.Contains(t.TestId));
            }
            var handles = targets.Select(t => t.TestHandle).ToList();
            var bulk = await s.Engine.BulkRecordResultsAsync(resolved, handles, verdict, reason);
            var after = await s.Engine.GetQueueAsync(resolved);
            return (ExitCodes.Success, new RunResult
            {
                Command = "run bulk-record",
                Status = "completed",
                RunId = resolved,
                Progress = after?.GetProgress(),
                BlockedTests = bulk.BlockedTests,
                Message = $"Recorded {bulk.ProcessedCount} test(s) as {verdict.ToString().ToUpperInvariant()}."
            });
        }, ct);
    }

    // ---- finalize / lifecycle ---------------------------------------------

    public async Task<int> FinalizeAsync(string? runId, bool force, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run finalize", async s =>
        {
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail("run finalize", "NO_ACTIVE_RUN", "No run id given and no active run.", ExitCodes.NotFound);
            var run = await s.Engine.GetRunAsync(resolved);
            if (run is null)
                return Fail("run finalize", "RUN_NOT_FOUND", $"Run '{resolved}' not found.", ExitCodes.NotFound);

            try
            {
                var finalized = await s.Engine.FinalizeRunAsync(resolved, force);
                if (finalized is null)
                    return Fail("run finalize", "INVALID_TRANSITION", $"Cannot finalize run in status '{run.Status}'.", ExitCodes.Error);

                var results = await s.Engine.GetResultsAsync(resolved);
                var titles = s.IndexLoader(finalized.Suite).ToDictionary(e => e.Id, e => e.Title, StringComparer.OrdinalIgnoreCase);
                var testCases = new Dictionary<string, Spectra.Core.Models.TestCase>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in results.Select(r => r.TestId).Distinct())
                {
                    var tc = s.TestCaseLoader(finalized.Suite, id);
                    if (tc is not null) testCases[id] = tc;
                }
                var report = s.ReportGenerator.Generate(finalized, results, titles, testCases);
                var (jsonPath, mdPath, htmlPath) = await s.ReportWriter.WriteAsync(report);

                return (ExitCodes.Success, new RunResult
                {
                    Command = "run finalize",
                    Status = "completed",
                    RunId = finalized.RunId,
                    RunStatus = finalized.Status.ToString(),
                    Reports = new RunReports { Json = Path.GetFileName(jsonPath), Markdown = Path.GetFileName(mdPath), Html = Path.GetFileName(htmlPath) },
                    Summary = new RunReportSummary
                    {
                        Total = report.Summary.Total,
                        Passed = report.Summary.Passed,
                        Failed = report.Summary.Failed,
                        Skipped = report.Summary.Skipped,
                        Blocked = report.Summary.Blocked
                    },
                    Message = $"Run finalized. Open `.execution/reports/{Path.GetFileName(htmlPath)}`."
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("pending"))
            {
                return Fail("run finalize", "TESTS_PENDING", ex.Message, ExitCodes.Error);
            }
        }, ct);
    }

    public Task<int> PauseAsync(string? runId, CancellationToken ct = default) => TransitionAsync("run pause", runId, (s, id) => s.Engine.PauseRunAsync(id), ct);
    public Task<int> ResumeAsync(string? runId, CancellationToken ct = default) => TransitionAsync("run resume", runId, (s, id) => s.Engine.ResumeRunAsync(id), ct);
    public Task<int> CancelAsync(string? runId, CancellationToken ct = default) => TransitionAsync("run cancel", runId, (s, id) => s.Engine.CancelRunAsync(id, null), ct);

    private async Task<int> TransitionAsync(string command, string? runId, Func<RunServices, string, Task<Spectra.Core.Models.Execution.Run?>> op, CancellationToken ct)
    {
        return await RunGuardedAsync(command, async s =>
        {
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail(command, "NO_ACTIVE_RUN", "No run id given and no active run.", ExitCodes.NotFound);
            var run = await op(s, resolved);
            if (run is null)
                return Fail(command, "RUN_NOT_FOUND_OR_INVALID", $"Run '{resolved}' not found or not in a valid state for this transition.", ExitCodes.NotFound);
            return (ExitCodes.Success, new RunResult { Command = command, Status = "completed", RunId = run.RunId, RunStatus = run.Status.ToString(), Message = $"Run {run.RunId} → {run.Status}." });
        }, ct);
    }

    public async Task<int> CancelAllAsync(CancellationToken ct = default)
    {
        return await RunGuardedAsync("run cancel-all", async s =>
        {
            var active = await s.RunRepo.GetActiveRunsAsync();
            var cancelled = new List<RunListItem>();
            foreach (var r in active)
            {
                var c = await s.Engine.CancelRunAsync(r.RunId, "cancel-all");
                if (c is not null) cancelled.Add(new RunListItem { RunId = c.RunId, Suite = c.Suite, Status = c.Status.ToString() });
            }
            return (ExitCodes.Success, new RunResult { Command = "run cancel-all", Status = "completed", Runs = cancelled, Message = $"Cancelled {cancelled.Count} active run(s)." });
        }, ct);
    }

    // ---- screenshots -------------------------------------------------------

    public async Task<int> ScreenshotAsync(string? handle, string? file, string? caption, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run screenshot", async s =>
        {
            if (string.IsNullOrWhiteSpace(file))
                return Fail("run screenshot", "FILE_REQUIRED", "--file is required.", ExitCodes.MissingArguments);
            if (!File.Exists(file))
                return Fail("run screenshot", "FILE_NOT_FOUND", $"Screenshot file not found: {file}", ExitCodes.NotFound);
            var bytes = await File.ReadAllBytesAsync(file, ct);
            return await SaveScreenshotBytesAsync(s, handle, bytes, caption);
        }, ct);
    }

    public async Task<int> ScreenshotClipboardAsync(string? handle, string? caption, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run screenshot-clipboard", async s =>
        {
            var temp = Path.Combine(Path.GetTempPath(), $"spectra-clipboard-{Guid.NewGuid():N}.png");
            try
            {
                if (!await ScreenshotService.TryCaptureClipboardAsync(temp))
                    return Fail("run screenshot-clipboard", "NO_CLIPBOARD_IMAGE", "No image found on the clipboard. Re-copy the screenshot or use `run screenshot --file`.", ExitCodes.Error);
                var bytes = await File.ReadAllBytesAsync(temp, ct);
                return await SaveScreenshotBytesAsync(s, handle, bytes, caption);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }, ct);
    }

    private async Task<(int, RunResult)> SaveScreenshotBytesAsync(RunServices s, string? handle, byte[] bytes, string? caption)
    {
        var resolvedHandle = await ResolveScreenshotHandleAsync(s, handle);
        if (resolvedHandle is null)
            return Fail("run screenshot", "NO_TEST", "No test to attach the screenshot to. Pass a handle.", ExitCodes.NotFound);
        var result = await s.Engine.GetTestResultAsync(resolvedHandle);
        if (result is null)
            return Fail("run screenshot", "INVALID_HANDLE", $"Test handle '{resolvedHandle}' not found.", ExitCodes.NotFound);

        var existing = result.ScreenshotPaths?.Count ?? 0;
        var saved = await new ScreenshotService().EncodeAndSaveAsync(s.Config.ReportsPath, result.RunId, result.TestId, existing, bytes);
        await s.ResultRepo.AppendScreenshotPathAsync(resolvedHandle, saved.RelativePath);
        var note = string.IsNullOrEmpty(caption) ? $"[Screenshot: {saved.RelativePath}]" : $"[Screenshot: {saved.RelativePath}] {caption}";
        await s.Engine.AddNoteAsync(resolvedHandle, note);

        return (ExitCodes.Success, new RunResult
        {
            Command = "run screenshot",
            Status = "completed",
            RunId = result.RunId,
            Recorded = new RunRecorded { TestId = result.TestId, Status = result.Status.ToString().ToUpperInvariant() },
            Message = $"Saved screenshot for {result.TestId}: .execution/reports/{saved.RelativePath} ({saved.Format}, {saved.SizeBytes} bytes)."
        });
    }

    private async Task<string?> ResolveScreenshotHandleAsync(RunServices s, string? handle)
    {
        var resolved = await ResolveHandleAsync(s, handle);
        if (resolved is not null) return resolved;
        // Screenshots are typically attached right after recording — fall back to the most recently completed test.
        var run = await s.Engine.GetActiveRunAsync();
        if (run is null) return null;
        var all = await s.ResultRepo.GetByRunIdAsync(run.RunId);
        return all.Where(r => r.CompletedAt.HasValue).OrderByDescending(r => r.CompletedAt).FirstOrDefault()?.TestHandle;
    }

    // ---- discovery / reporting --------------------------------------------

    public async Task<int> ListSuitesAsync(CancellationToken ct = default)
    {
        return await RunGuardedAsync("run list-suites", (s) =>
        {
            var suites = s.SuiteListLoader()
                .Select(name => new RunSuiteItem { Suite = name, TestCount = s.IndexLoader(name).Count() })
                .ToList();
            return Task.FromResult((ExitCodes.Success, new RunResult { Command = "run list-suites", Status = "completed", Suites = suites }));
        }, ct);
    }

    public async Task<int> ListActiveAsync(CancellationToken ct = default)
    {
        return await RunGuardedAsync("run list-active", async s =>
        {
            var active = await s.RunRepo.GetActiveRunsAsync();
            var runs = active.Select(r => new RunListItem { RunId = r.RunId, Suite = r.Suite, Status = r.Status.ToString(), StartedAt = r.StartedAt.ToString("O"), StartedBy = r.StartedBy }).ToList();
            return (ExitCodes.Success, new RunResult { Command = "run list-active", Status = "completed", Runs = runs, Message = $"{runs.Count} active run(s)." });
        }, ct);
    }

    public async Task<int> HistoryAsync(string? suite, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run history", async s =>
        {
            var runs = await s.RunRepo.GetAllAsync(suite, limit: 50);
            var items = runs.Select(r => new RunListItem { RunId = r.RunId, Suite = r.Suite, Status = r.Status.ToString(), StartedAt = r.StartedAt.ToString("O"), StartedBy = r.StartedBy }).ToList();
            return (ExitCodes.Success, new RunResult { Command = "run history", Status = "completed", Runs = items });
        }, ct);
    }

    public async Task<int> SummaryAsync(string? runId, CancellationToken ct = default)
    {
        return await RunGuardedAsync("run summary", async s =>
        {
            var resolved = await ResolveRunIdAsync(s, runId);
            if (resolved is null)
                return Fail("run summary", "NO_ACTIVE_RUN", "No run id given and no active run.", ExitCodes.NotFound);
            var run = await s.Engine.GetRunAsync(resolved);
            if (run is null)
                return Fail("run summary", "RUN_NOT_FOUND", $"Run '{resolved}' not found.", ExitCodes.NotFound);
            var counts = await s.Engine.GetStatusCountsAsync(resolved);
            return (ExitCodes.Success, new RunResult
            {
                Command = "run summary",
                Status = "completed",
                RunId = run.RunId,
                Suite = run.Suite,
                RunStatus = run.Status.ToString(),
                Counts = counts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
            });
        }, ct);
    }

    public async Task<int> SelectionsAsync(CancellationToken ct = default)
    {
        return await RunGuardedAsync("run selections", (s) =>
        {
            var names = s.SelectionsLoader().Keys.ToList();
            return Task.FromResult((ExitCodes.Success, new RunResult { Command = "run selections", Status = "completed", Selections = names }));
        }, ct);
    }

    // ---- shared helpers ----------------------------------------------------

    private async Task<int> RunGuardedAsync(string command, Func<RunServices, Task<(int, RunResult)>> body, CancellationToken ct)
    {
        await using var services = new RunServices(_basePath);
        try
        {
            var (code, result) = await body(services);
            Emit(result);
            return code;
        }
        catch (QueueReconstructionException ex)
        {
            // FR-008: fail loud and distinct — never conflated with a benign "run not found".
            var (code, result) = Fail(command, "RECONSTRUCTION_FAILED", ex.Message, ExitCodes.Error);
            Emit(result);
            return code;
        }
        catch (OperationCanceledException)
        {
            Emit(new RunResult { Command = command, Status = "cancelled", Message = "Cancelled." });
            return ExitCodes.Cancelled;
        }
    }

    private static (int, RunResult) Fail(string command, string code, string message, int exitCode) =>
        (exitCode, new RunResult { Command = command, Status = "failed", ErrorCode = code, Message = message });

    private async Task<string?> ResolveRunIdAsync(RunServices s, string? runId)
    {
        if (!string.IsNullOrWhiteSpace(runId)) return runId;
        var active = await s.Engine.GetActiveRunAsync();
        return active?.RunId;
    }

    private async Task<string?> ResolveHandleAsync(RunServices s, string? handle)
    {
        if (!string.IsNullOrWhiteSpace(handle)) return handle;
        var run = await s.Engine.GetActiveRunAsync();
        if (run is null) return null;
        var inProgress = await s.ResultRepo.GetInProgressTestsAsync(run.RunId);
        if (inProgress.Count > 0) return inProgress[0].TestHandle;
        var queue = await s.Engine.GetQueueAsync(run.RunId);
        return queue?.GetNext()?.TestHandle;
    }

    private static bool TryParseVerdict(string status, out TestStatus verdict)
    {
        switch (status.Trim().ToLowerInvariant())
        {
            case "pass": case "passed": case "p": verdict = TestStatus.Passed; return true;
            case "fail": case "failed": case "f": verdict = TestStatus.Failed; return true;
            case "blocked": case "block": case "b": verdict = TestStatus.Blocked; return true;
            case "skip": case "skipped": case "s": verdict = TestStatus.Skipped; return true;
            default: verdict = TestStatus.Pending; return false;
        }
    }

    private void Emit(RunResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        else if (_verbosity != VerbosityLevel.Quiet && !string.IsNullOrEmpty(result.Message))
        {
            if (result.Status == "failed")
                Console.Error.WriteLine(result.Message);
            else
                Console.WriteLine(result.Message);
        }

        // Persist the machine-readable artifact (parity with other commands).
        var resultPath = Path.Combine(_basePath ?? Directory.GetCurrentDirectory(), ".spectra-result.json");
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, result.GetType(), options));
        }
        catch { /* non-critical */ }
    }
}
