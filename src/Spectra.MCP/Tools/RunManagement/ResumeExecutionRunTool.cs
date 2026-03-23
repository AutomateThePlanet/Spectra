using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: resume_execution_run
/// Resumes a paused run.
/// </summary>
public sealed class ResumeExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;

    public string Description => "Resumes a paused execution run. If run_id is omitted, auto-detects when exactly one active run exists.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run to resume. If omitted, auto-detects when exactly one active run exists." }
        }
    };

    public ResumeExecutionRunTool(ExecutionEngine engine, RunRepository runRepo)
    {
        _engine = engine;
        _runRepo = runRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<ResumeExecutionRunRequest>(parameters);

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

        if (!await _engine.VerifyOwnerAsync(resolvedRunId!))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NOT_OWNER",
                "Run belongs to a different user",
                run.Status,
                "get_execution_status"));
        }

        if (run.Status != RunStatus.Paused)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TRANSITION",
                $"Cannot resume run in status '{run.Status}'",
                run.Status,
                GetNextActionForStatus(run.Status)));
        }

        var resumedRun = await _engine.ResumeRunAsync(resolvedRunId!);
        if (resumedRun is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TRANSITION",
                "Failed to resume run",
                run.Status));
        }

        var queue = await _engine.GetQueueAsync(resolvedRunId!);
        var progress = queue?.GetProgress() ?? "?/?";
        var nextTest = queue?.GetNext();

        var data = new
        {
            run_id = resumedRun.RunId,
            next_test = nextTest is not null ? new
            {
                test_handle = nextTest.TestHandle,
                test_id = nextTest.TestId,
                title = nextTest.Title
            } : null
        };

        var nextAction = nextTest is not null ? "get_test_case_details" : "finalize_execution_run";

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            RunStatus.Running,
            progress,
            nextAction));
    }

    private static string GetNextActionForStatus(RunStatus status) => status switch
    {
        RunStatus.Paused => "resume_execution_run",
        RunStatus.Running => "get_test_case_details",
        RunStatus.Completed => "start_execution_run",
        RunStatus.Cancelled => "start_execution_run",
        _ => "get_execution_status"
    };
}

internal sealed class ResumeExecutionRunRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }
}
