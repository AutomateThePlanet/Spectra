using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: get_execution_status
/// Returns current run state and progress.
/// </summary>
public sealed class GetExecutionStatusTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Returns current run state and progress";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier" }
        },
        required = new[] { "run_id" }
    };

    public GetExecutionStatusTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetExecutionStatusRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.RunId))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "run_id is required"));
        }

        var run = await _engine.GetRunAsync(request.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{request.RunId}' not found"));
        }

        var statusCounts = await _engine.GetStatusCountsAsync(request.RunId);
        var summary = ReportSummary.FromCounts(statusCounts);
        var queue = await _engine.GetQueueAsync(request.RunId);

        var currentTest = queue?.GetNext();
        var progress = queue?.GetProgress() ?? $"{summary.Total - summary.Pending}/{summary.Total}";

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
                title = currentTest.Title
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

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            run.Status,
            progress,
            nextAction));
    }
}

internal sealed class GetExecutionStatusRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }
}
