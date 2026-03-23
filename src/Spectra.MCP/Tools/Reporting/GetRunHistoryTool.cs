using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.Reporting;

/// <summary>
/// MCP tool: get_run_history
/// Returns history of execution runs with optional filters.
/// </summary>
public sealed class GetRunHistoryTool : IMcpTool
{
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;

    public string Description => "Returns history of execution runs with optional filters for status, suite, and limit";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new { type = "string", description = "Filter by suite name" },
            user = new { type = "string", description = "Filter by user who started the run" },
            status = new { type = "string", description = "Filter by run status (CREATED, RUNNING, PAUSED, COMPLETED, CANCELLED, ABANDONED)" },
            limit = new { type = "integer", description = "Maximum number of runs to return (default: 50)" }
        }
    };

    public GetRunHistoryTool(RunRepository runRepo, ResultRepository resultRepo)
    {
        _runRepo = runRepo;
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetRunHistoryRequest>(parameters);

        // Parse status filter if provided
        RunStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request?.Status))
        {
            if (!Enum.TryParse<RunStatus>(request.Status, true, out var parsed))
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "INVALID_STATUS",
                    $"Invalid status '{request.Status}'. Must be one of: CREATED, RUNNING, PAUSED, COMPLETED, CANCELLED, ABANDONED"));
            }
            statusFilter = parsed;
        }

        var limit = request?.Limit ?? 50;
        var runs = await _runRepo.GetAllAsync(request?.Suite, request?.User, limit, statusFilter);

        var runData = new List<object>();
        foreach (var r in runs)
        {
            var counts = await _resultRepo.GetStatusCountsAsync(r.RunId);
            var total = counts.Values.Sum();

            runData.Add(new
            {
                run_id = r.RunId,
                suite = r.Suite,
                status = r.Status.ToString(),
                started_at = r.StartedAt.ToString("O"),
                started_by = r.StartedBy,
                completed_at = r.CompletedAt?.ToString("O"),
                environment = r.Environment,
                summary = new
                {
                    total,
                    passed = counts.GetValueOrDefault(TestStatus.Passed),
                    failed = counts.GetValueOrDefault(TestStatus.Failed),
                    skipped = counts.GetValueOrDefault(TestStatus.Skipped),
                    blocked = counts.GetValueOrDefault(TestStatus.Blocked)
                }
            });
        }

        var data = new
        {
            runs = runData,
            count = runData.Count
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            nextExpectedAction: "get_execution_summary"));
    }
}

internal sealed class GetRunHistoryRequest
{
    [JsonPropertyName("suite")]
    public string? Suite { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}
