using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Reports;

namespace Spectra.CLI.Commands.Run.WebConsole;

/// <summary>
/// Spec 066: the execution console's write-back surface — a sibling of <c>RunHandler</c> and the MCP
/// tools over the SAME long-lived <see cref="RunServices"/> / <see cref="ExecutionEngine"/> / SQLite.
/// Each method dispatches 1:1 to an existing engine operation; the browser is a view + write-back caller
/// (FR-002/FR-003). The verdict guardrails replicate <c>RunHandler.AdvanceAsync</c> verbatim (FR-005), so
/// the DB state after a console write-back is indistinguishable from the CLI/engine path (FR-004/FR-013).
///
/// This type is transport-free and directly unit-testable: <see cref="ConsoleServer"/> only parses HTTP
/// into these calls and serializes <see cref="ConsoleResponse"/> back out.
/// </summary>
public sealed class ConsoleEndpoints
{
    private readonly RunServices _s;

    public ConsoleEndpoints(RunServices services) => _s = services;

    // ---- GET /current -------------------------------------------------------

    /// <summary>The single read the page polls — a pure projection of <c>GetStatusAsync</c> (FR-002).</summary>
    public Task<ConsoleResponse> GetCurrentAsync() => GuardAsync(async () =>
    {
        var run = await _s.Engine.GetActiveRunAsync();
        if (run is null)
            return Ok(new { runStatus = "none", runId = (string?)null, current = (object?)null, results = Array.Empty<object>(), message = "No active run. Start one with `spectra run start <suite>`." });

        var status = await _s.Engine.GetStatusAsync(run.RunId);
        if (status is null)
            return Ok(new { runStatus = "none", runId = (string?)null, current = (object?)null, results = Array.Empty<object>(), message = "Run not found." });

        var (r, queue) = status.Value;
        var counts = await _s.Engine.GetStatusCountsAsync(run.RunId);
        var current = await BuildCurrentAsync(r.Suite, run.RunId, queue);
        var results = (await _s.Engine.GetResultsAsync(run.RunId))
            .Select(x => new { testId = x.TestId, status = x.Status.ToString().ToUpperInvariant(), notes = x.Notes, screenshotPaths = x.ScreenshotPaths ?? new List<string>() })
            .ToList();

        return Ok(new
        {
            runStatus = r.Status.ToString(),
            runId = r.RunId,
            suite = r.Suite,
            total = queue.TotalCount,
            counts = counts.ToDictionary(kv => kv.Key.ToString().ToUpperInvariant(), kv => kv.Value),
            current,
            results
        });
    });

    private async Task<object?> BuildCurrentAsync(string suite, string runId, TestQueue queue)
    {
        var inProgress = await _s.ResultRepo.GetInProgressTestsAsync(runId);
        var target = inProgress.Count > 0 ? queue.GetByHandle(inProgress[0].TestHandle) : queue.GetNext();
        if (target is null) return null;

        var tc = _s.TestCaseLoader(suite, target.TestId);
        var result = await _s.Engine.GetTestResultAsync(target.TestHandle);
        return new
        {
            testHandle = target.TestHandle,
            testId = target.TestId,
            title = tc?.Title ?? target.Title,
            priority = tc?.Priority,
            component = tc?.Component,
            preconditions = tc?.Preconditions,
            steps = tc?.Steps,
            expectedResult = tc?.ExpectedResult,
            status = result?.Status.ToString(),
            notes = result?.Notes,
            screenshotPaths = result?.ScreenshotPaths ?? new List<string>()
        };
    }

    // ---- POST /advance ------------------------------------------------------

    /// <summary>
    /// Records a verdict and advances. Replicates the mechanical guardrails of
    /// <c>RunHandler.AdvanceAsync</c> (RunHandler.cs:204-211): explicit status required; notes required
    /// for fail/blocked/skip; the console never infers a verdict (FR-005).
    /// </summary>
    public Task<ConsoleResponse> AdvanceAsync(string? status, string? notes) => GuardAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(status))
            return Err(400, "STATUS_REQUIRED", "A verdict (pass|fail|blocked|skip) is required. The console never advances without an explicit verdict.");
        if (!TryParseVerdict(status, out var verdict))
            return Err(400, "INVALID_STATUS", "Status must be one of: pass, fail, blocked, skip.");
        if (verdict is TestStatus.Failed or TestStatus.Blocked or TestStatus.Skipped && string.IsNullOrWhiteSpace(notes))
            return Err(400, "NOTES_REQUIRED", $"Notes/reason are required for {verdict.ToString().ToUpperInvariant()}.");

        var run = await _s.Engine.GetActiveRunAsync();
        if (run is null) return Err(404, "NO_ACTIVE_RUN", "No active run to record against.");

        var handle = await ResolveHandleAsync(run.RunId);
        if (handle is null) return Err(404, "NO_TEST", "No in-progress or next-pending test to record.");

        var result = await _s.Engine.GetTestResultAsync(handle);
        if (result is null) return Err(404, "INVALID_HANDLE", $"Test handle '{handle}' not found.");

        // Auto-start a pending target (parity with RunHandler / advance_test_case).
        if (result.Status == TestStatus.Pending)
        {
            await _s.Engine.StartTestAsync(run.RunId, handle);
            result = await _s.Engine.GetTestResultAsync(handle);
        }
        if (result is null || result.Status != TestStatus.InProgress)
            return Err(409, "TEST_NOT_IN_PROGRESS", $"Test is not in progress (status: {result?.Status}).");

        try
        {
            var (recorded, blocked, next) = await _s.Engine.AdvanceTestAsync(run.RunId, handle, verdict, notes);
            var current = await CurrentBodyAsync();
            return Ok(new
            {
                recorded = new { testId = recorded.TestId, status = recorded.Status.ToString().ToUpperInvariant() },
                blocked,
                next = next is null ? null : new { testHandle = next.TestHandle, testId = next.TestId, title = next.Title },
                current
            });
        }
        catch (InvalidOperationException ex)
        {
            return Err(409, "INVALID_TRANSITION", ex.Message);
        }
    });

    // ---- POST /note ---------------------------------------------------------

    public Task<ConsoleResponse> NoteAsync(string? note) => GuardAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(note))
            return Err(400, "NOTE_REQUIRED", "A note is required.");
        var run = await _s.Engine.GetActiveRunAsync();
        if (run is null) return Err(404, "NO_ACTIVE_RUN", "No active run.");
        var handle = await ResolveHandleAsync(run.RunId);
        if (handle is null) return Err(404, "NO_TEST", "No test to annotate.");
        var result = await _s.Engine.GetTestResultAsync(handle);
        if (result is null) return Err(404, "INVALID_HANDLE", $"Test handle '{handle}' not found.");
        await _s.Engine.AddNoteAsync(handle, note);
        return Ok(new { added = true, current = await CurrentBodyAsync() });
    });

    // ---- POST /finalize -----------------------------------------------------

    public Task<ConsoleResponse> FinalizeAsync(bool force) => GuardAsync(async () =>
    {
        var run = await _s.Engine.GetActiveRunAsync();
        if (run is null) return Err(404, "NO_ACTIVE_RUN", "No active run to finalize.");
        try
        {
            var finalized = await _s.Engine.FinalizeRunAsync(run.RunId, force);
            if (finalized is null) return Err(409, "INVALID_TRANSITION", $"Cannot finalize run in status '{run.Status}'.");

            var results = await _s.Engine.GetResultsAsync(run.RunId);
            var titles = _s.IndexLoader(finalized.Suite).ToDictionary(e => e.Id, e => e.Title, StringComparer.OrdinalIgnoreCase);
            var testCases = new Dictionary<string, Spectra.Core.Models.TestCase>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in results.Select(r => r.TestId).Distinct())
            {
                var tc = _s.TestCaseLoader(finalized.Suite, id);
                if (tc is not null) testCases[id] = tc;
            }
            var report = _s.ReportGenerator.Generate(finalized, results, titles, testCases);
            var (_, _, htmlPath) = await _s.ReportWriter.WriteAsync(report);
            return Ok(new { runStatus = finalized.Status.ToString(), report = Path.GetFileName(htmlPath) });
        }
        catch (InvalidOperationException ex)
        {
            return Err(409, "TESTS_PENDING", ex.Message);
        }
    });

    // ---- POST /screenshot ---------------------------------------------------

    /// <summary>
    /// Browser→bytes ingest (FR-006): the only new screenshot surface. Reuses
    /// <see cref="ScreenshotService.EncodeAndSaveAsync"/> + <c>AppendScreenshotPathAsync</c> verbatim;
    /// no new storage model, no browser-side authoritative copy. The HTTP layer decodes multipart /
    /// data-URL into <paramref name="imageBytes"/> before calling this.
    /// </summary>
    public Task<ConsoleResponse> ScreenshotAsync(byte[]? imageBytes) => GuardAsync(async () =>
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return Err(400, "SCREENSHOT_INVALID", "No image bytes received.");

        var run = await _s.Engine.GetActiveRunAsync();
        if (run is null) return Err(404, "NO_ACTIVE_RUN", "No active run to attach a screenshot to.");

        var handle = await ResolveScreenshotHandleAsync(run.RunId);
        if (handle is null) return Err(404, "NO_TEST", "No test to attach the screenshot to.");
        var result = await _s.Engine.GetTestResultAsync(handle);
        if (result is null) return Err(404, "INVALID_HANDLE", $"Test handle '{handle}' not found.");

        var existing = result.ScreenshotPaths?.Count ?? 0;
        var saved = await new ScreenshotService().EncodeAndSaveAsync(_s.Config.ReportsPath, result.RunId, result.TestId, existing, imageBytes);
        await _s.ResultRepo.AppendScreenshotPathAsync(handle, saved.RelativePath);

        var refreshed = await _s.Engine.GetTestResultAsync(handle);
        return Ok(new { saved = saved.RelativePath, screenshotPaths = refreshed?.ScreenshotPaths ?? new List<string>() });
    });

    // ---- POST /retest -------------------------------------------------------

    /// <summary>
    /// Exposes <see cref="ExecutionEngine.RetestAsync"/> to the console — no new engine mechanics,
    /// pure delegation. The engine appends a new attempt row and requeues the test as Pending.
    /// </summary>
    public Task<ConsoleResponse> RetestAsync(string? testId) => GuardAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(testId))
            return Err(400, "TEST_ID_REQUIRED", "testId is required.");
        var run = await _s.Engine.GetActiveRunAsync();
        if (run is null) return Err(404, "NO_ACTIVE_RUN", "No active run.");
        var result = await _s.Engine.RetestAsync(run.RunId, testId);
        if (result is null) return Err(404, "NOT_FOUND", $"Test '{testId}' not found in run or has no prior attempt.");
        return Ok(new { queued = new { testId = result.TestId, attempt = result.Attempt }, current = await CurrentBodyAsync() });
    });

    // ---- helpers ------------------------------------------------------------

    private async Task<object?> CurrentBodyAsync()
    {
        var resp = await GetCurrentAsync();
        return resp.Body;
    }

    private async Task<string?> ResolveHandleAsync(string runId)
    {
        var inProgress = await _s.ResultRepo.GetInProgressTestsAsync(runId);
        if (inProgress.Count > 0) return inProgress[0].TestHandle;
        var queue = await _s.Engine.GetQueueAsync(runId);
        return queue?.GetNext()?.TestHandle;
    }

    private async Task<string?> ResolveScreenshotHandleAsync(string runId)
    {
        var resolved = await ResolveHandleAsync(runId);
        if (resolved is not null) return resolved;
        // Mirror RunHandler: a screenshot attached right after a verdict targets the last completed test.
        var all = await _s.ResultRepo.GetByRunIdAsync(runId);
        return all.Where(r => r.CompletedAt.HasValue).OrderByDescending(r => r.CompletedAt).FirstOrDefault()?.TestHandle;
    }

    private static async Task<ConsoleResponse> GuardAsync(Func<Task<ConsoleResponse>> body)
    {
        try { return await body(); }
        catch (QueueReconstructionException ex)
        {
            // FR-008 / spec 064: fail loud and distinct — never conflated with a benign "run not found".
            return Err(400, "RECONSTRUCTION_FAILED", ex.Message);
        }
    }

    private static ConsoleResponse Ok(object body) => new(200, body);
    private static ConsoleResponse Err(int code, string errorCode, string message) => new(code, new ConsoleError(errorCode, message));

    private static bool TryParseVerdict(string status, out TestStatus verdict)
    {
        switch (status.Trim().ToLowerInvariant())
        {
            case "pass": case "passed": verdict = TestStatus.Passed; return true;
            case "fail": case "failed": verdict = TestStatus.Failed; return true;
            case "blocked": verdict = TestStatus.Blocked; return true;
            case "skip": case "skipped": verdict = TestStatus.Skipped; return true;
            default: verdict = TestStatus.Pending; return false;
        }
    }
}

/// <summary>A transport-free endpoint result: an HTTP status code + a body to serialize as JSON.</summary>
public sealed record ConsoleResponse(int StatusCode, object? Body);

/// <summary>The shared 4xx error envelope (contract: <c>{ error_code, message }</c>).</summary>
public sealed record ConsoleError(
    [property: JsonPropertyName("error_code")] string ErrorCode,
    [property: JsonPropertyName("message")] string Message);
