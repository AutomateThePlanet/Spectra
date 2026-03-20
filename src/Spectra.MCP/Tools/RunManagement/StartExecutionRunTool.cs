using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tools.RunManagement;

/// <summary>
/// MCP tool: start_execution_run
/// Creates a new execution run for a suite, custom test IDs, or saved selection.
/// </summary>
public sealed class StartExecutionRunTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly Func<string, IEnumerable<TestIndexEntry>> _indexLoader;
    private readonly Func<IEnumerable<string>>? _suiteListLoader;
    private readonly Func<IReadOnlyDictionary<string, SavedSelectionConfig>>? _selectionsLoader;

    public string Description => "Creates a new execution run for a test suite, custom test IDs, or saved selection";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new { type = "string", description = "Suite name (mutually exclusive with test_ids and selection)" },
            test_ids = new { type = "array", items = new { type = "string" }, description = "Run specific tests from any suites (mutually exclusive with suite and selection)" },
            selection = new { type = "string", description = "Run a saved selection by name (mutually exclusive with suite and test_ids)" },
            name = new { type = "string", description = "Run name (required for test_ids and selection modes)" },
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
        }
    };

    public StartExecutionRunTool(ExecutionEngine engine, Func<string, IEnumerable<TestIndexEntry>> indexLoader)
    {
        _engine = engine;
        _indexLoader = indexLoader;
    }

    public StartExecutionRunTool(
        ExecutionEngine engine,
        Func<string, IEnumerable<TestIndexEntry>> indexLoader,
        Func<IEnumerable<string>> suiteListLoader,
        Func<IReadOnlyDictionary<string, SavedSelectionConfig>> selectionsLoader)
    {
        _engine = engine;
        _indexLoader = indexLoader;
        _suiteListLoader = suiteListLoader;
        _selectionsLoader = selectionsLoader;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<StartExecutionRunRequest>(parameters);

        // Determine mode
        var hasSuite = !string.IsNullOrEmpty(request?.Suite);
        var hasTestIds = request?.TestIds is { Count: > 0 };
        var hasSelection = !string.IsNullOrEmpty(request?.Selection);

        var modeCount = (hasSuite ? 1 : 0) + (hasTestIds ? 1 : 0) + (hasSelection ? 1 : 0);

        if (modeCount == 0)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "One of suite, test_ids, or selection is required"));
        }

        if (modeCount > 1)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "Parameters suite, test_ids, and selection are mutually exclusive"));
        }

        if ((hasTestIds || hasSelection) && string.IsNullOrEmpty(request?.Name))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "A run name is required when using test_ids or selection"));
        }

        try
        {
            if (hasTestIds)
                return await ExecuteWithTestIdsAsync(request!);
            if (hasSelection)
                return await ExecuteWithSelectionAsync(request!);
            return await ExecuteWithSuiteAsync(request!);
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

    private async Task<string> ExecuteWithSuiteAsync(StartExecutionRunRequest request)
    {
        if (string.IsNullOrEmpty(request.Suite))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "Suite name is required"));
        }

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

        return FormatRunResponse(run, queue);
    }

    private async Task<string> ExecuteWithTestIdsAsync(StartExecutionRunRequest request)
    {
        if (_suiteListLoader is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NOT_CONFIGURED",
                "Test ID mode not available — server not configured with suite list loader"));
        }

        var testIds = request.TestIds!;

        // Deduplicate preserving order
        var seen = new HashSet<string>();
        var uniqueIds = new List<string>();
        foreach (var id in testIds)
        {
            if (seen.Add(id))
                uniqueIds.Add(id);
        }

        // Resolve test IDs across all suite indexes
        var allEntries = new Dictionary<string, (string Suite, TestIndexEntry Entry)>();
        foreach (var suite in _suiteListLoader())
        {
            foreach (var entry in _indexLoader(suite))
            {
                allEntries.TryAdd(entry.Id, (suite, entry));
            }
        }

        // Validate all IDs exist
        var invalidIds = uniqueIds.Where(id => !allEntries.ContainsKey(id)).ToList();
        if (invalidIds.Count > 0)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_TEST_IDS",
                $"Test IDs not found: {string.Join(", ", invalidIds)}"));
        }

        // Build entries in specified order
        var orderedEntries = uniqueIds.Select(id => allEntries[id].Entry).ToList();
        var suites = uniqueIds.Select(id => allEntries[id].Suite).Distinct().ToList();
        var suiteName = string.Join("+", suites);

        var (run, queue) = await _engine.StartRunAsync(
            suiteName,
            orderedEntries,
            request.Environment);

        return FormatRunResponse(run, queue);
    }

    private async Task<string> ExecuteWithSelectionAsync(StartExecutionRunRequest request)
    {
        if (_suiteListLoader is null || _selectionsLoader is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NOT_CONFIGURED",
                "Selection mode not available — server not configured with selections loader"));
        }

        var selections = _selectionsLoader();
        if (!selections.TryGetValue(request.Selection!, out var selectionConfig))
        {
            var available = string.Join(", ", selections.Keys);
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "SELECTION_NOT_FOUND",
                $"Selection '{request.Selection}' not found. Available: {available}"));
        }

        // Load all tests and apply selection filters
        var allTests = new List<TestIndexEntry>();
        foreach (var suite in _suiteListLoader())
        {
            try { allTests.AddRange(_indexLoader(suite)); }
            catch { /* skip malformed */ }
        }

        var matched = ListSavedSelectionsTool.ApplyFilters(allTests, selectionConfig);
        if (matched.Count == 0)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_TESTS_MATCHED",
                $"Selection '{request.Selection}' matched zero tests"));
        }

        var suiteName = $"selection:{request.Selection}";
        var (run, queue) = await _engine.StartRunAsync(
            suiteName,
            matched,
            request.Environment);

        return FormatRunResponse(run, queue);
    }

    private static string FormatRunResponse(Run run, TestQueue queue)
    {
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
}

internal sealed class StartExecutionRunRequest
{
    [JsonPropertyName("suite")]
    public string? Suite { get; set; }

    [JsonPropertyName("test_ids")]
    public List<string>? TestIds { get; set; }

    [JsonPropertyName("selection")]
    public string? Selection { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

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
