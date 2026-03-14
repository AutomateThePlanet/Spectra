using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.Reporting;

/// <summary>
/// MCP tool: get_execution_summary
/// Returns summary statistics for a specific execution run.
/// </summary>
public sealed class GetExecutionSummaryTool : IMcpTool
{
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public string Description => "Returns summary statistics for a specific execution run";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier" }
        },
        required = new[] { "run_id" }
    };

    public GetExecutionSummaryTool(RunRepository runRepo, ResultRepository resultRepo)
    {
        _runRepo = runRepo;
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetExecutionSummaryRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.RunId))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "run_id is required"));
        }

        var run = await _runRepo.GetByIdAsync(request.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{request.RunId}' not found"));
        }

        var statusCounts = await _resultRepo.GetStatusCountsAsync(request.RunId);
        var total = statusCounts.Values.Sum();
        var passed = statusCounts.GetValueOrDefault(TestStatus.Passed, 0);
        var failed = statusCounts.GetValueOrDefault(TestStatus.Failed, 0);
        var skipped = statusCounts.GetValueOrDefault(TestStatus.Skipped, 0);
        var blocked = statusCounts.GetValueOrDefault(TestStatus.Blocked, 0);
        var completed = passed + failed + skipped + blocked;

        var passRate = total > 0 ? Math.Round((double)passed / total * 100, 1) : 0;

        var data = new
        {
            run_id = run.RunId,
            suite = run.Suite,
            status = run.Status.ToString(),
            started_at = run.StartedAt.ToString("O"),
            completed_at = run.CompletedAt?.ToString("O"),
            started_by = run.StartedBy,
            environment = run.Environment,
            summary = new
            {
                total,
                passed,
                failed,
                skipped,
                blocked,
                pass_rate = passRate
            }
        };

        var progress = $"{completed}/{total}";
        var nextAction = run.Status == RunStatus.Completed
            ? "start_execution_run"
            : "get_test_case_details";

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            run.Status,
            progress,
            nextAction));
    }
}

internal sealed class GetExecutionSummaryRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }
}
