using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: skip_test_case
/// Skips current test with reason.
/// </summary>
public sealed class SkipTestCaseTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public string Description => "Skips the current test with a reason. Auto-detects the active test if test_handle is omitted. Use ONLY for SKIP — for BLOCKED tests, use advance_test_case with status=BLOCKED instead";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle to skip (auto-detected from active run if omitted)" },
            reason = new { type = "string", description = "Reason for skipping (REQUIRED)" }
        },
        required = new[] { "reason" }
    };

    public SkipTestCaseTool(ExecutionEngine engine, RunRepository runRepo, ResultRepository resultRepo)
    {
        _engine = engine;
        _runRepo = runRepo;
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<SkipTestCaseRequest>(parameters);

        // Resolve test_handle
        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);
        string? resolvedTestHandle = request?.TestHandle;
        if (string.IsNullOrEmpty(resolvedTestHandle))
        {
            if (runError is not null)
                return runError;
            var (autoHandle, handleError) = await ActiveRunResolver.ResolveTestHandleAsync(null, resolvedRunId!, _resultRepo);
            if (handleError is not null)
                return handleError;
            resolvedTestHandle = autoHandle;
        }

        if (string.IsNullOrEmpty(request?.Reason))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "reason is required"));
        }

        var result = await _engine.GetTestResultAsync(resolvedTestHandle);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{resolvedTestHandle}' not found"));
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
            var (skipped, blocked, next) = await _engine.SkipTestAsync(
                result.RunId,
                resolvedTestHandle,
                request?.Reason);

            var queue = await _engine.GetQueueAsync(result.RunId);
            var progress = queue?.GetProgress() ?? "?/?";

            var data = new
            {
                skipped = new
                {
                    test_id = skipped.TestId,
                    reason = request?.Reason
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

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                data,
                run.Status,
                progress,
                nextAction));
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

internal sealed class SkipTestCaseRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
