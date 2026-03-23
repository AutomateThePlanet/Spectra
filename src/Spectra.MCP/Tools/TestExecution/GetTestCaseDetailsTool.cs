using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: get_test_case_details
/// Returns full test content for execution.
/// </summary>
public sealed class GetTestCaseDetailsTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly Func<string, string, TestCase?> _testLoader;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public string Description => "Returns full test content for execution. Auto-detects test_handle from the active run when omitted.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle from start_execution_run or advance_test_case. Auto-detected from the active run when omitted." }
        }
    };

    public GetTestCaseDetailsTool(ExecutionEngine engine, Func<string, string, TestCase?> testLoader, RunRepository runRepo, ResultRepository resultRepo)
    {
        _engine = engine;
        _testLoader = testLoader;
        _runRepo = runRepo;
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetTestCaseDetailsRequest>(parameters);

        // Resolve test_handle — for this tool, we need the NEXT test (pending or in-progress),
        // not just in-progress tests, because this tool is what transitions pending → in-progress.
        string? resolvedTestHandle = request?.TestHandle;
        if (string.IsNullOrEmpty(resolvedTestHandle))
        {
            var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);
            if (runError is not null)
                return runError;

            var resolveQueue = await _engine.GetQueueAsync(resolvedRunId!);
            var nextTest = resolveQueue?.GetNext();
            if (nextTest is null)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "NO_PENDING_TESTS",
                    "No pending or in-progress tests remaining. Use finalize_execution_run to complete the run.",
                    nextExpectedAction: "finalize_execution_run"));
            }
            resolvedTestHandle = nextTest.TestHandle;
        }

        var result = await _engine.GetTestResultAsync(resolvedTestHandle!);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{resolvedTestHandle}' not found"));
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
        await _engine.StartTestAsync(result.RunId, resolvedTestHandle!);

        // Load full test case content
        var testCase = _testLoader(run.Suite, result.TestId);

        var queue = await _engine.GetQueueAsync(result.RunId);
        var progress = queue?.GetProgress() ?? "?/?";

        var data = new
        {
            test_handle = resolvedTestHandle,
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
