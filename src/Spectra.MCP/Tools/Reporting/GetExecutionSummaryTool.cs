using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools;

namespace Spectra.MCP.Tools.Reporting;

/// <summary>
/// MCP tool: get_execution_summary
/// Returns summary statistics for a specific execution run.
/// </summary>
public sealed class GetExecutionSummaryTool : IMcpTool
{
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public string Description => "Returns summary statistics for a specific execution run. If run_id is omitted, auto-detects when exactly one active run exists.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier (optional — auto-detected when exactly one active run exists)" }
        }
    };

    public GetExecutionSummaryTool(RunRepository runRepo, ResultRepository resultRepo)
    {
        _runRepo = runRepo;
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetExecutionSummaryRequest>(parameters);

        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(request?.RunId, _runRepo);
        if (runError is not null)
            return runError;

        var run = await _runRepo.GetByIdAsync(resolvedRunId!);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{resolvedRunId!}' not found"));
        }

        var statusCounts = await _resultRepo.GetStatusCountsAsync(resolvedRunId!);
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
