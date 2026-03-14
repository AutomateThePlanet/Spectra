using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: cancel_execution_run
/// Cancels an active execution run.
/// </summary>
public sealed class CancelExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Cancels an active execution run";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier" },
            reason = new { type = "string", description = "Reason for cancellation" }
        },
        required = new[] { "run_id" }
    };

    public CancelExecutionRunTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<CancelExecutionRunRequest>(parameters);
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

        var cancelled = await _engine.CancelRunAsync(request.RunId, request.Reason);
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
            reason = request.Reason
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
