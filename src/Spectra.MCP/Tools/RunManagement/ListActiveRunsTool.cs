using System.Text.Json;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: list_active_runs
/// Lists all active (non-terminal) execution runs.
/// </summary>
public sealed class ListActiveRunsTool : IMcpTool
{
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public string Description => "List all active (non-terminal) execution runs. Returns runs in CREATED, RUNNING, or PAUSED state.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new { }
    };

    public ListActiveRunsTool(RunRepository runRepo, ResultRepository resultRepo)
    {
        _runRepo = runRepo;
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var activeRuns = await _runRepo.GetActiveRunsAsync();

        if (activeRuns.Count == 0)
        {
            var emptyData = new
            {
                runs = Array.Empty<object>(),
                count = 0,
                message = "No active runs found."
            };
            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                emptyData,
                nextExpectedAction: "start_execution_run"));
        }

        var runEntries = new List<object>();
        foreach (var run in activeRuns)
        {
            var counts = await _resultRepo.GetStatusCountsAsync(run.RunId);
            var total = counts.Values.Sum();
            var completed = total - counts.GetValueOrDefault(TestStatus.Pending) - counts.GetValueOrDefault(TestStatus.InProgress);
            var passed = counts.GetValueOrDefault(TestStatus.Passed);
            var failed = counts.GetValueOrDefault(TestStatus.Failed);

            var progress = total > 0
                ? $"{completed}/{total} completed, {passed} passed, {failed} failed"
                : "0/0 completed";

            runEntries.Add(new
            {
                run_id = run.RunId,
                suite = run.Suite,
                status = run.Status.ToString(),
                started_at = run.StartedAt.ToString("O"),
                started_by = run.StartedBy,
                environment = run.Environment,
                progress
            });
        }

        var data = new
        {
            runs = runEntries,
            count = runEntries.Count
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            nextExpectedAction: "get_execution_status"));
    }
}
