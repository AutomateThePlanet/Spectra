using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: cancel_execution_run
/// Cancels an active execution run.
/// </summary>
public sealed class CancelExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;

    public string Description => "Cancels an execution run. If run_id is omitted, auto-detects when exactly one active run exists.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run to cancel. If omitted, auto-detects when exactly one active run exists." },
            reason = new { type = "string", description = "Reason for cancellation" }
        }
    };

    public CancelExecutionRunTool(ExecutionEngine engine, RunRepository runRepo)
    {
        _engine = engine;
        _runRepo = runRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<CancelExecutionRunRequest>(parameters);

        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(request?.RunId, _runRepo);
        if (runError is not null)
            return runError;

        var run = await _engine.GetRunAsync(resolvedRunId!);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                $"Run '{resolvedRunId!}' not found"));
        }

        var cancelled = await _engine.CancelRunAsync(resolvedRunId!, request?.Reason);
        if (cancelled is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TRANSITION",
                $"Cannot cancel run in status '{run.Status}'",
                run.Status,
                GetNextActionForStatus(run.Status)));
        }

        var data = new
        {
            run_id = cancelled.RunId,
            cancelled_at = DateTime.UtcNow.ToString("O"),
            reason = request?.Reason
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            RunStatus.Cancelled,
            nextExpectedAction: "start_execution_run"));
    }

    private static string GetNextActionForStatus(RunStatus status)
    {
        return status switch
        {
            RunStatus.Running => "finalize_execution_run",
            RunStatus.Paused => "resume_execution_run",
            RunStatus.Completed => "start_execution_run",
            RunStatus.Cancelled => "start_execution_run",
            _ => "get_execution_status"
        };
    }
}

internal sealed class CancelExecutionRunRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
