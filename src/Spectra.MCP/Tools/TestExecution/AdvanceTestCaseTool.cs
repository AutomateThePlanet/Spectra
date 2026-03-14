using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: advance_test_case
/// Records result and returns next test.
/// </summary>
public sealed class AdvanceTestCaseTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Records test result and returns the next test";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Current test handle" },
            status = new { type = "string", @enum = new[] { "PASSED", "FAILED" }, description = "Test result" },
            notes = new { type = "string", description = "Optional observations" }
        },
        required = new[] { "test_handle", "status" }
    };

    public AdvanceTestCaseTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<AdvanceTestCaseRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.TestHandle))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "test_handle is required"));
        }

        if (string.IsNullOrEmpty(request.Status))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "status is required"));
        }

        if (!Enum.TryParse<TestStatus>(request.Status, true, out var status) ||
            (status != TestStatus.Passed && status != TestStatus.Failed))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_STATUS",
                "Status must be PASSED or FAILED"));
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
            var (recorded, blocked, next) = await _engine.AdvanceTestAsync(
                result.RunId,
                request.TestHandle,
                status,
                request.Notes);

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

internal sealed class AdvanceTestCaseRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
