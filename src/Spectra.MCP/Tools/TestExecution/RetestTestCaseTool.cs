using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: retest_test_case
/// Requeues a completed test for another attempt.
/// </summary>
public sealed class RetestTestCaseTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Requeues a completed test for another attempt";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run ID" },
            test_id = new { type = "string", description = "Test ID to retest" }
        },
        required = new[] { "run_id", "test_id" }
    };

    public RetestTestCaseTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<RetestRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.RunId) || string.IsNullOrEmpty(request.TestId))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "Run ID and test ID are required"));
        }

        var run = await _engine.GetRunAsync(request.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{request.RunId}' not found"));
        }

        // Check if test exists and is completed
        var status = await _engine.GetStatusAsync(request.RunId);
        if (status is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{request.RunId}' not found or not active"));
        }

        var existingTest = status.Value.Queue.GetById(request.TestId);
        if (existingTest is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TEST_NOT_FOUND",
                $"Test '{request.TestId}' not found in run"));
        }

        // Check test is completed (not pending or in progress)
        if (!StateMachine.IsTerminal(existingTest.Status))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TEST_NOT_COMPLETED",
                $"Test '{request.TestId}' has not been completed yet (status: {existingTest.Status})"));
        }

        // Retest the test
        var requeued = await _engine.RetestAsync(request.RunId, request.TestId);
        if (requeued is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RETEST_FAILED",
                $"Failed to requeue test '{request.TestId}'"));
        }

        var queue = (await _engine.GetStatusAsync(request.RunId))!.Value.Queue;

        var data = new
        {
            test_id = requeued.TestId,
            test_handle = requeued.TestHandle,
            attempt = requeued.Attempt,
            message = $"Test '{request.TestId}' requeued for attempt {requeued.Attempt}"
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            run.Status,
            queue.GetProgress(),
            "get_test_case_details"));
    }
}

internal sealed class RetestRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }

    [JsonPropertyName("test_id")]
    public string? TestId { get; set; }
}
