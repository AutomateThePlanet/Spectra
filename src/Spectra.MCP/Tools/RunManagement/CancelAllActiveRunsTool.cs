using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: cancel_all_active_runs
/// Cancels all active execution runs at once.
/// </summary>
public sealed class CancelAllActiveRunsTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;

    public string Description => "Cancel all active execution runs at once. Transitions all CREATED, RUNNING, and PAUSED runs to CANCELLED.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            reason = new { type = "string", description = "Optional reason for cancellation" }
        }
    };

    public CancelAllActiveRunsTool(ExecutionEngine engine, RunRepository runRepo)
    {
        _engine = engine;
        _runRepo = runRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<CancelAllActiveRunsRequest>(parameters);
        var activeRuns = await _runRepo.GetActiveRunsAsync();

        if (activeRuns.Count == 0)
        {
            var emptyData = new
            {
                cancelled = Array.Empty<object>(),
                cancelled_count = 0,
                failed = Array.Empty<object>(),
                message = "No active runs to cancel."
            };
            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                emptyData,
                nextExpectedAction: "start_execution_run"));
        }

        var cancelled = new List<object>();
        var failed = new List<object>();

        foreach (var run in activeRuns)
        {
            try
            {
                var result = await _engine.CancelRunAsync(run.RunId, request?.Reason);
                if (result is not null)
                {
                    cancelled.Add(new
                    {
                        run_id = run.RunId,
                        suite = run.Suite,
                        previous_status = run.Status.ToString()
                    });
                }
                else
                {
                    failed.Add(new
                    {
                        run_id = run.RunId,
                        suite = run.Suite,
                        reason = $"Failed to cancel run in status '{run.Status}'"
                    });
                }
            }
            catch (Exception ex)
            {
                failed.Add(new
                {
                    run_id = run.RunId,
                    suite = run.Suite,
                    reason = ex.Message
                });
            }
        }

        var data = new
        {
            cancelled,
            cancelled_count = cancelled.Count,
            failed
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            nextExpectedAction: "start_execution_run"));
    }
}

internal sealed class CancelAllActiveRunsRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
