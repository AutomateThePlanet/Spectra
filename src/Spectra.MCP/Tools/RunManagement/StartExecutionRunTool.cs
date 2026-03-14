using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: start_execution_run
/// Creates a new execution run for a suite.
/// </summary>
public sealed class StartExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly Func<string, IEnumerable<TestIndexEntry>> _indexLoader;

    public string Description => "Creates a new execution run for a test suite";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new { type = "string", description = "Suite name" },
            environment = new { type = "string", description = "Target environment" },
            filters = new
            {
                type = "object",
                properties = new
                {
                    priority = new { type = "string" },
                    tags = new { type = "array", items = new { type = "string" } },
                    component = new { type = "string" },
                    test_ids = new { type = "array", items = new { type = "string" } }
                }
            }
        },
        required = new[] { "suite" }
    };

    public StartExecutionRunTool(ExecutionEngine engine, Func<string, IEnumerable<TestIndexEntry>> indexLoader)
    {
        _engine = engine;
        _indexLoader = indexLoader;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<StartExecutionRunRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.Suite))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "Suite name is required"));
        }

        try
        {
            // Load test index for the suite
            var entries = _indexLoader(request.Suite).ToList();
            if (entries.Count == 0)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "INVALID_SUITE",
                    $"Suite '{request.Suite}' not found or has no tests"));
            }

            var filters = request.Filters is not null ? new RunFilters
            {
                Priority = request.Filters.Priority is not null
                    ? Enum.Parse<Priority>(request.Filters.Priority, true)
                    : null,
                Tags = request.Filters.Tags,
                Component = request.Filters.Component,
                TestIds = request.Filters.TestIds
            } : null;

            var (run, queue) = await _engine.StartRunAsync(
                request.Suite,
                entries,
                request.Environment,
                filters);

            var firstTest = queue.GetNext();

            var data = new
            {
                run_id = run.RunId,
                suite = run.Suite,
                test_count = queue.TotalCount,
                first_test = firstTest is not null ? new
                {
                    test_handle = firstTest.TestHandle,
                    test_id = firstTest.TestId,
                    title = firstTest.Title
                } : null
            };

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                data,
                RunStatus.Running,
                queue.GetProgress(),
                "get_test_case_details"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Active run exists"))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "ACTIVE_RUN_EXISTS",
                ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No tests match"))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_TESTS_MATCH",
                ex.Message));
        }
    }
}

internal sealed class StartExecutionRunRequest
{
    [JsonPropertyName("suite")]
    public string? Suite { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("filters")]
    public StartExecutionRunFilters? Filters { get; set; }
}

internal sealed class StartExecutionRunFilters
{
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("component")]
    public string? Component { get; set; }

    [JsonPropertyName("test_ids")]
    public List<string>? TestIds { get; set; }
}
