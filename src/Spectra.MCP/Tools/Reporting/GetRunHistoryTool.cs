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

    public string Description => "Returns history of execution runs with optional filters";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new { type = "string", description = "Filter by suite name" },
            user = new { type = "string", description = "Filter by user who started the run" },
            limit = new { type = "integer", description = "Maximum number of runs to return (default: 50)" }
        }
    };

    public GetRunHistoryTool(RunRepository runRepo)
    {
        _runRepo = runRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetRunHistoryRequest>(parameters);

        var limit = request?.Limit ?? 50;
        var runs = await _runRepo.GetAllAsync(request?.Suite, request?.User, limit);

        var runData = runs.Select(r => new
        {
            run_id = r.RunId,
            suite = r.Suite,
            status = r.Status.ToString(),
            started_at = r.StartedAt.ToString("O"),
            started_by = r.StartedBy,
            completed_at = r.CompletedAt?.ToString("O"),
            environment = r.Environment
        }).ToList();

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

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}
