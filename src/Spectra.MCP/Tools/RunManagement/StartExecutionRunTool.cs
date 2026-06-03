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

            // Canonical top-level filter shape — identical to find_test_cases.
            priorities = new { type = "array", items = new { type = "string" }, description = "Filter by priority (OR within array). Same shape as find_test_cases." },
            tags = new { type = "array", items = new { type = "string" }, description = "Filter by tags (OR within array). Same shape as find_test_cases." },
            components = new { type = "array", items = new { type = "string" }, description = "Filter by component (OR within array). Same shape as find_test_cases." },

            filters = new
            {
                type = "object",
                deprecated = true,
                description = "DEPRECATED — use top-level priorities/tags/components instead. Still honored this release.",
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
        var request = McpProtocol.DeserializeParams<StartExecutionRunRequest>(parameters, "start_execution_run");

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

        var warnings = new List<string>();
        var filters = NormalizeFilters(request, warnings);

        var (run, queue) = await _engine.StartRunAsync(
            request.Suite,
            entries,
            request.Environment,
            filters);

        return FormatRunResponse(run, queue, warnings);
    }

    /// <summary>
    /// Spec 051: resolves whichever filter shape arrived into one <see cref="RunFilters"/>.
    /// Top-level plural fields (matching find_test_cases) are canonical and win when
    /// both shapes are present; the legacy nested <c>filters</c> object is honored
    /// (deprecated) as a fallback and lifted singular→plural.
    /// </summary>
    private static RunFilters? NormalizeFilters(StartExecutionRunRequest request, List<string> warnings)
    {
        var hasTopLevel = request.Priorities is { Count: > 0 }
            || request.Tags is { Count: > 0 }
            || request.Components is { Count: > 0 };

#pragma warning disable CS0618 // legacy nested shape intentionally honored (deprecated)
        var legacy = request.Filters;
#pragma warning restore CS0618

        if (hasTopLevel)
        {
            if (legacy is not null)
            {
                warnings.Add("Both top-level and nested 'filters' provided; using the top-level priorities/tags/components.");
            }

            return RunFilters.From(request.Priorities, request.Tags, request.Components);
        }

        if (legacy is not null)
        {
            // Lift the singular legacy shape into the unified plural model.
            return new RunFilters
            {
                Priority = legacy.Priority is not null
                    ? Enum.Parse<Priority>(legacy.Priority, true)
                    : null,
                Tags = legacy.Tags,
                Component = legacy.Component,
                TestIds = legacy.TestIds
            };
        }

        return null;
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

    private static string FormatRunResponse(Run run, TestQueue queue, List<string>? warnings = null)
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
            } : null,
            warnings = warnings is { Count: > 0 } ? warnings : null
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

    // Spec 051: canonical top-level filter shape (matches find_test_cases).
    [JsonPropertyName("priorities")]
    public List<string>? Priorities { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("components")]
    public List<string>? Components { get; set; }

    // Spec 051: legacy nested shape — deprecated, still honored this release.
    [Obsolete("Use top-level priorities/tags/components instead.")]
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
