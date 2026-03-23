using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: retest_test_case
/// Requeues a completed test for another attempt.
/// </summary>
public sealed class RetestTestCaseTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;

    public string Description => "Requeues a completed test for another attempt. If run_id is omitted, auto-detects when exactly one active run exists.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run ID (auto-detected if omitted and exactly one active run exists)" },
            test_id = new { type = "string", description = "Test ID to retest" }
        },
        required = new[] { "test_id" }
    };

    public RetestTestCaseTool(ExecutionEngine engine, RunRepository runRepo)
    {
        _engine = engine;
        _runRepo = runRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<RetestRequest>(parameters);

        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(request?.RunId, _runRepo);
        if (runError is not null)
            return runError;

        if (request is null || string.IsNullOrEmpty(request.TestId))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "test_id is required"));
        }

        var run = await _engine.GetRunAsync(resolvedRunId!);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{resolvedRunId!}' not found"));
        }

        // Check if test exists and is completed
        var status = await _engine.GetStatusAsync(resolvedRunId!);
        if (status is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{resolvedRunId!}' not found or not active"));
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
        var requeued = await _engine.RetestAsync(resolvedRunId!, request.TestId);
        if (requeued is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RETEST_FAILED",
                $"Failed to requeue test '{request.TestId}'"));
        }

        var queue = (await _engine.GetStatusAsync(resolvedRunId!))!.Value.Queue;

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
