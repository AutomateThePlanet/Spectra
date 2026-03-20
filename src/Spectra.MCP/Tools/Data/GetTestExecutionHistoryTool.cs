using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.Data;

/// <summary>
/// MCP tool: get_test_execution_history
/// Returns per-test execution statistics for smart prioritization.
/// Pure deterministic — queries SQLite execution database.
/// </summary>
public sealed class GetTestExecutionHistoryTool : IMcpTool
{
    private readonly ResultRepository _resultRepo;

    public string Description => "Get execution history and statistics for specific tests";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_ids = new { type = "array", items = new { type = "string" }, description = "Test IDs to query. If omitted, returns all tests with history." },
            limit = new { type = "integer", description = "Max recent executions per test for statistics (default: 10)" }
        }
    };

    public GetTestExecutionHistoryTool(ResultRepository resultRepo)
    {
        _resultRepo = resultRepo;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<GetTestExecutionHistoryRequest>(parameters);

        var testIds = request?.TestIds;
        var limit = request?.Limit ?? 10;

        try
        {
            var history = await _resultRepo.GetTestExecutionHistoryAsync(testIds, limit);

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(history));
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "HISTORY_ERROR",
                $"Failed to retrieve execution history: {ex.Message}"));
        }
    }
}

internal sealed class GetTestExecutionHistoryRequest
{
    [JsonPropertyName("test_ids")]
    public List<string>? TestIds { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}
