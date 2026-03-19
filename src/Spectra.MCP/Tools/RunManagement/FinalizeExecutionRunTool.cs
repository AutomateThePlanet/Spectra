using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Reports;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: finalize_execution_run
/// Completes the run and generates reports.
/// </summary>
public sealed class FinalizeExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly ReportGenerator _reportGenerator;
    private readonly ReportWriter _reportWriter;

    public string Description => "Completes the run and generates reports";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier" },
            force = new { type = "boolean", description = "Allow finalize with pending tests" }
        },
        required = new[] { "run_id" }
    };

    public FinalizeExecutionRunTool(
        ExecutionEngine engine,
        ReportGenerator reportGenerator,
        ReportWriter reportWriter)
    {
        _engine = engine;
        _reportGenerator = reportGenerator;
        _reportWriter = reportWriter;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<FinalizeExecutionRunRequest>(parameters);
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

        try
        {
            var finalizedRun = await _engine.FinalizeRunAsync(request.RunId, request.Force);
            if (finalizedRun is null)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "INVALID_TRANSITION",
                    $"Cannot finalize run in status '{run.Status}'",
                    run.Status,
                    GetNextActionForStatus(run.Status)));
            }

            // Generate and write report
            var results = await _engine.GetResultsAsync(request.RunId);
            var report = _reportGenerator.Generate(finalizedRun, results);
            var (jsonPath, mdPath, htmlPath) = await _reportWriter.WriteAsync(report);

            var data = new
            {
                run_id = finalizedRun.RunId,
                completed_at = finalizedRun.CompletedAt?.ToString("O"),
                reports = new
                {
                    json = jsonPath,
                    markdown = mdPath,
                    html = htmlPath
                },
                summary = new
                {
                    total = report.Summary.Total,
                    passed = report.Summary.Passed,
                    failed = report.Summary.Failed,
                    skipped = report.Summary.Skipped,
                    blocked = report.Summary.Blocked
                }
            };

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                data,
                RunStatus.Completed,
                $"{report.Summary.Total}/{report.Summary.Total}",
                "start_execution_run"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("pending"))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TESTS_PENDING",
                ex.Message,
                run.Status,
                "advance_test_case"));
        }
    }

    private static string GetNextActionForStatus(RunStatus status)
    {
        return status switch
        {
            RunStatus.Paused => "resume_execution_run",
            RunStatus.Completed => "start_execution_run",
            RunStatus.Cancelled => "start_execution_run",
            _ => "get_execution_status"
        };
    }
}

internal sealed class FinalizeExecutionRunRequest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }

    [JsonPropertyName("force")]
    public bool Force { get; set; }
}
