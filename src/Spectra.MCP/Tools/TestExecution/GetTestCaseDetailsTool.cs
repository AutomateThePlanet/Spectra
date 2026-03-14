using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: get_test_case_details
/// Returns full test content for execution.
/// </summary>
public sealed class GetTestCaseDetailsTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly Func<string, string, TestCase?> _testLoader;

    public string Description => "Returns full test content for execution";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle from start_execution_run or advance_test_case" }
        },
        required = new[] { "test_handle" }
    };

    public GetTestCaseDetailsTool(ExecutionEngine engine, Func<string, string, TestCase?> testLoader)
    {
        _engine = engine;
        _testLoader = testLoader;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetTestCaseDetailsRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.TestHandle))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "test_handle is required"));
        }

        var result = await _engine.GetTestResultAsync(request.TestHandle);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{request.TestHandle}' not found"));
        }

        if (StateMachine.IsTerminal(result.Status))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TEST_NOT_PENDING",
                $"Test '{result.TestId}' has already been completed with status '{result.Status}'"));
        }

        var run = await _engine.GetRunAsync(result.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run not found for handle"));
        }

        // Mark test as in progress
        await _engine.StartTestAsync(result.RunId, request.TestHandle);

        // Load full test case content
        var testCase = _testLoader(run.Suite, result.TestId);

        var queue = await _engine.GetQueueAsync(result.RunId);
        var progress = queue?.GetProgress() ?? "?/?";

        var data = new
        {
            test_handle = request.TestHandle,
            test_id = result.TestId,
            title = testCase?.Title ?? result.TestId,
            priority = testCase?.Priority.ToString().ToLowerInvariant() ?? "medium",
            tags = testCase?.Tags ?? Array.Empty<string>(),
            component = testCase?.Component,
            preconditions = testCase?.Preconditions,
            step_count = testCase?.Steps?.Count ?? 0,
            steps = testCase?.Steps?.Select((s, i) => new
            {
                number = i + 1,
                action = s
            }) ?? Enumerable.Empty<object>(),
            expected_result = testCase?.ExpectedResult,
            test_data = testCase?.TestData
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            run.Status,
            progress,
            "advance_test_case"));
    }
}

internal sealed class GetTestCaseDetailsRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }
}
