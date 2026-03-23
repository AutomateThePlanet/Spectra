using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: get_execution_status
/// Returns current run state, progress, and inline test details for resilience after context compaction.
/// </summary>
public sealed class GetExecutionStatusTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;
    private readonly Func<string, string, TestCase?>? _testLoader;

    public string Description => "Returns current run state, progress, and full test details for the current test. If run_id is omitted, auto-detects when exactly one active run exists.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier. If omitted, auto-detects when exactly one active run exists." }
        }
    };

    public GetExecutionStatusTool(ExecutionEngine engine, RunRepository runRepo, Func<string, string, TestCase?>? testLoader = null)
    {
        _engine = engine;
        _runRepo = runRepo;
        _testLoader = testLoader;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetExecutionStatusRequest>(parameters);

        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(request?.RunId, _runRepo);
        if (runError is not null)
            return runError;

        var run = await _engine.GetRunAsync(resolvedRunId!);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{resolvedRunId}' not found"));
        }

        var statusCounts = await _engine.GetStatusCountsAsync(resolvedRunId!);
        var summary = ReportSummary.FromCounts(statusCounts);
        var queue = await _engine.GetQueueAsync(resolvedRunId!);

        var currentTest = queue?.GetNext();
        var progress = queue?.GetProgress() ?? $"{summary.Total - summary.Pending}/{summary.Total}";

        // Load inline test details when a test loader is available and there's a current test
        object? testDetails = null;
        if (currentTest is not null && _testLoader is not null)
        {
            var testCase = _testLoader(run.Suite, currentTest.TestId);
            if (testCase is not null)
            {
                testDetails = new
                {
                    preconditions = testCase.Preconditions,
                    steps = testCase.Steps?.Select((s, i) => new { number = i + 1, action = s }),
                    expected_result = testCase.ExpectedResult,
                    step_count = testCase.Steps?.Count ?? 0
                };
            }
        }

        var data = new
        {
            run_id = run.RunId,
            suite = run.Suite,
            started_at = run.StartedAt.ToString("O"),
            started_by = run.StartedBy,
            current_test = currentTest is not null ? new
            {
                test_handle = currentTest.TestHandle,
                test_id = currentTest.TestId,
                title = currentTest.Title,
                details = testDetails
            } : null,
            summary = new
            {
                total = summary.Total,
                passed = summary.Passed,
                failed = summary.Failed,
                skipped = summary.Skipped,
                blocked = summary.Blocked,
                pending = summary.Pending
            }
        };

        var nextAction = run.Status switch
        {
            RunStatus.Running when currentTest is not null => "get_test_case_details",
            RunStatus.Running => "finalize_execution_run",
            RunStatus.Paused => "resume_execution_run",
            _ => "start_execution_run"
        };

        // Build explicit instruction text to guide models after context compaction
        var instruction = run.Status switch
        {
            RunStatus.Running when currentTest is not null =>
                $"NEXT STEP: Call advance_test_case with status PASSED, FAILED, or BLOCKED for test {currentTest.TestId} (\"{currentTest.Title}\"). " +
                $"You can also call get_test_case_details first to review the full test steps. " +
                $"Progress: {progress}.",
            RunStatus.Running =>
                "All tests have been executed. Call finalize_execution_run to complete the run and generate reports.",
            RunStatus.Paused =>
                "The run is paused. Call resume_execution_run to continue testing.",
            _ => "No active run. Call start_execution_run to begin a new execution."
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            run.Status,
            progress,
            nextAction,
            instruction));
    }
}

internal sealed class GetExecutionStatusRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }
}
