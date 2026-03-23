using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Reports;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

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
    private readonly Func<string, IEnumerable<TestIndexEntry>> _indexLoader;
    private readonly Func<string, string, TestCase?>? _testCaseLoader;
    private readonly RunRepository _runRepo;

    public string Description => "Completes the run and generates reports. If run_id is omitted, auto-detects when exactly one active run exists.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "string", description = "Run identifier. If omitted, auto-detects when exactly one active run exists." },
            force = new { type = "boolean", description = "Allow finalize with pending tests" }
        }
    };

    public FinalizeExecutionRunTool(
        ExecutionEngine engine,
        ReportGenerator reportGenerator,
        ReportWriter reportWriter,
        Func<string, IEnumerable<TestIndexEntry>> indexLoader,
        Func<string, string, TestCase?>? testCaseLoader = null,
        RunRepository? runRepo = null)
    {
        _engine = engine;
        _reportGenerator = reportGenerator;
        _reportWriter = reportWriter;
        _indexLoader = indexLoader;
        _testCaseLoader = testCaseLoader;
        _runRepo = runRepo!;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<FinalizeExecutionRunRequest>(parameters);

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

        try
        {
            var finalizedRun = await _engine.FinalizeRunAsync(resolvedRunId!, request?.Force ?? false);
            if (finalizedRun is null)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "INVALID_TRANSITION",
                    $"Cannot finalize run in status '{run.Status}'",
                    run.Status,
                    GetNextActionForStatus(run.Status)));
            }

            // Generate and write report
            var results = await _engine.GetResultsAsync(resolvedRunId!);

            // Load test titles from index
            var testTitles = _indexLoader(finalizedRun.Suite)
                .ToDictionary(e => e.Id, e => e.Title, StringComparer.OrdinalIgnoreCase);

            // Load full test cases for report content
            Dictionary<string, TestCase>? testCases = null;
            if (_testCaseLoader is not null)
            {
                testCases = new Dictionary<string, TestCase>(StringComparer.OrdinalIgnoreCase);
                foreach (var testId in results.Select(r => r.TestId).Distinct())
                {
                    var tc = _testCaseLoader(finalizedRun.Suite, testId);
                    if (tc is not null)
                    {
                        testCases[testId] = tc;
                    }
                }
            }

            var report = _reportGenerator.Generate(finalizedRun, results, testTitles, testCases);
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
