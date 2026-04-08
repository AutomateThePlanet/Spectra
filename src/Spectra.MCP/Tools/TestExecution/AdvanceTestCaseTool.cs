using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: advance_test_case
/// Records result and returns next test.
/// </summary>
public sealed class AdvanceTestCaseTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly ResultRepository? _resultRepo;
    private readonly RunRepository _runRepo;

    public string Description => "Records test result and returns the next test. Auto-detects the current in-progress test if test_handle is omitted.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Current test handle. If omitted, auto-detects the current in-progress test." },
            status = new { type = "string", @enum = new[] { "PASSED", "FAILED", "BLOCKED" }, description = "Test result" },
            notes = new { type = "string", description = "Observations (REQUIRED for FAILED and BLOCKED tests)" },
            screenshot_paths = new { type = "array", items = new { type = "string" }, description = "Optional list of screenshot relative paths to attach" }
        },
        required = new[] { "status" }
    };

    public AdvanceTestCaseTool(ExecutionEngine engine, ResultRepository resultRepo, RunRepository runRepo)
    {
        _engine = engine;
        _resultRepo = resultRepo;
        _runRepo = runRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<AdvanceTestCaseRequest>(parameters);

        // Resolve test_handle: if omitted, auto-detect from active run's in-progress test,
        // or fall back to the next pending test in the queue (resilience after context compaction).
        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);
        string? resolvedTestHandle = request?.TestHandle;
        if (string.IsNullOrEmpty(resolvedTestHandle))
        {
            if (runError is not null)
                return runError;

            var (autoHandle, handleError) = await ActiveRunResolver.ResolveTestHandleAsync(null, resolvedRunId!, _resultRepo!);
            if (handleError is not null)
            {
                // No in-progress test found — try the next pending test from the queue.
                // This handles the case where get_test_case_details was never called
                // (e.g., after context compaction the model skips it).
                var queue = await _engine.GetQueueAsync(resolvedRunId!);
                var nextPending = queue?.GetNext();
                if (nextPending is not null)
                {
                    resolvedTestHandle = nextPending.TestHandle;
                }
                else
                {
                    return handleError;
                }
            }
            else
            {
                resolvedTestHandle = autoHandle;
            }
        }

        if (string.IsNullOrEmpty(request?.Status))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "status is required"));
        }

        if (!Enum.TryParse<TestStatus>(request.Status, true, out var status) ||
            status is not (TestStatus.Passed or TestStatus.Failed or TestStatus.Blocked))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_STATUS",
                "Status must be PASSED, FAILED, or BLOCKED"));
        }

        // Notes are required for failed and blocked tests
        if (status is TestStatus.Failed or TestStatus.Blocked && string.IsNullOrWhiteSpace(request?.Notes))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NOTES_REQUIRED",
                $"Notes are required for {status.ToString().ToUpperInvariant()} tests - explain why the test {(status == TestStatus.Failed ? "failed" : "is blocked")}"));
        }

        var result = await _engine.GetTestResultAsync(resolvedTestHandle!);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{resolvedTestHandle}' not found"));
        }

        // Auto-start pending tests — eliminates the hard requirement to call get_test_case_details first.
        // This is critical for resilience after context compaction where models skip the details step.
        if (result.Status == TestStatus.Pending)
        {
            await _engine.StartTestAsync(result.RunId, resolvedTestHandle!);
            // Re-fetch to get the updated status
            result = await _engine.GetTestResultAsync(resolvedTestHandle!);
            if (result is null || result.Status != TestStatus.InProgress)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "AUTO_START_FAILED",
                    $"Failed to auto-start test '{resolvedTestHandle}'. Use get_test_case_details to start the test manually."));
            }
        }

        if (result.Status != TestStatus.InProgress)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TEST_NOT_IN_PROGRESS",
                $"Test '{result.TestId}' is not in progress (status: {result.Status})"));
        }

        var run = await _engine.GetRunAsync(result.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                "Run not found"));
        }

        try
        {
            var (recorded, blocked, next) = await _engine.AdvanceTestAsync(
                result.RunId,
                resolvedTestHandle!,
                status,
                request?.Notes);

            // Store screenshot paths if provided
            if (_resultRepo is not null && request?.ScreenshotPaths is { Count: > 0 })
            {
                foreach (var ssPath in request.ScreenshotPaths)
                {
                    await _resultRepo.AppendScreenshotPathAsync(resolvedTestHandle!, ssPath);
                }
            }

            var queue = await _engine.GetQueueAsync(result.RunId);
            var progress = queue?.GetProgress() ?? "?/?";

            var data = new
            {
                recorded = new
                {
                    test_id = recorded.TestId,
                    status = recorded.Status.ToString().ToUpperInvariant()
                },
                blocked_tests = blocked,
                next = next is not null ? new
                {
                    test_handle = next.TestHandle,
                    test_id = next.TestId,
                    title = next.Title
                } : null
            };

            var nextAction = next is not null ? "get_test_case_details" : "finalize_execution_run";

            var instruction = next is not null
                ? $"NEXT STEP: Call get_test_case_details for test {next.TestId} to get the full steps, preconditions, and expected result. " +
                  $"You MUST present the full test details to the user before asking for a result. Progress: {progress}."
                : "All tests have been executed. Call finalize_execution_run to complete the run and generate reports.";

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                data,
                run.Status,
                progress,
                nextAction,
                instruction));
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TRANSITION",
                ex.Message,
                run.Status));
        }
    }
}

internal sealed class AdvanceTestCaseRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("screenshot_paths")]
    public List<string>? ScreenshotPaths { get; set; }
}
