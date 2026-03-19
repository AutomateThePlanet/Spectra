using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: skip_test_case
/// Skips current test with reason.
/// </summary>
public sealed class SkipTestCaseTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Skips the current test with a reason";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle to skip" },
            reason = new { type = "string", description = "Reason for skipping (REQUIRED)" },
            blocked = new { type = "boolean", description = "Mark as BLOCKED (dependency failed) rather than SKIPPED", @default = false }
        },
        required = new[] { "test_handle", "reason" }
    };

    public SkipTestCaseTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<SkipTestCaseRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.TestHandle))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "test_handle is required"));
        }

        if (string.IsNullOrEmpty(request.Reason))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "reason is required"));
        }

        var result = await _engine.GetTestResultAsync(request.TestHandle);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{request.TestHandle}' not found"));
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
                request.TestHandle,
                request.Reason,
                request.Blocked);

            var queue = await _engine.GetQueueAsync(result.RunId);
            var progress = queue?.GetProgress() ?? "?/?";

            var data = new
            {
                skipped = new
                {
                    test_id = skipped.TestId,
                    reason = request.Reason
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

    [JsonPropertyName("blocked")]
    public bool Blocked { get; set; }
}
