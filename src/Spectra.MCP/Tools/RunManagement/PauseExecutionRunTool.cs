using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: pause_execution_run
/// Pauses an active run.
/// </summary>
public sealed class PauseExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Pauses an active execution run";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run to pause" }
        },
        required = new[] { "run_id" }
    };

    public PauseExecutionRunTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<PauseExecutionRunRequest>(parameters);
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

        if (!await _engine.VerifyOwnerAsync(request.RunId))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NOT_OWNER",
                "Run belongs to a different user",
                run.Status,
                "get_execution_status"));
        }

        if (run.Status != RunStatus.Running)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TRANSITION",
                $"Cannot pause run in status '{run.Status}'",
                run.Status,
                GetNextActionForStatus(run.Status)));
        }

        var pausedRun = await _engine.PauseRunAsync(request.RunId);
        if (pausedRun is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TRANSITION",
                "Failed to pause run",
                run.Status));
        }

        var queue = await _engine.GetQueueAsync(request.RunId);
        var progress = queue?.GetProgress() ?? "?/?";

        var data = new
        {
            run_id = pausedRun.RunId,
            paused_at = pausedRun.UpdatedAt.ToString("O")
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            RunStatus.Paused,
            progress,
            "resume_execution_run"));
    }

    private static string GetNextActionForStatus(RunStatus status) => status switch
    {
        RunStatus.Paused => "resume_execution_run",
        RunStatus.Running => "pause_execution_run",
        RunStatus.Completed => "start_execution_run",
        RunStatus.Cancelled => "start_execution_run",
        _ => "get_execution_status"
    };
}

internal sealed class PauseExecutionRunRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }
}
