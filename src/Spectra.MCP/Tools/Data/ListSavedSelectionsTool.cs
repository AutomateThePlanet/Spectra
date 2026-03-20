using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.Data;

/// <summary>
/// MCP tool: list_saved_selections
/// Lists saved test selections from spectra.config.json with estimated test counts.
/// Pure deterministic — reads config and evaluates filters.
/// </summary>
public sealed class ListSavedSelectionsTool : IMcpTool
{
    private readonly Func<IReadOnlyDictionary<string, SavedSelectionConfig>> _selectionsLoader;
    private readonly Func<IEnumerable<string>> _suiteListLoader;
    private readonly Func<string, IEnumerable<TestIndexEntry>> _indexLoader;

    public string Description => "List saved test selections from configuration";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new { }
    };

    public ListSavedSelectionsTool(
        Func<IReadOnlyDictionary<string, SavedSelectionConfig>> selectionsLoader,
        Func<IEnumerable<string>> suiteListLoader,
        Func<string, IEnumerable<TestIndexEntry>> indexLoader)
    {
        _selectionsLoader = selectionsLoader;
        _suiteListLoader = suiteListLoader;
        _indexLoader = indexLoader;
    }

    public Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var selections = _selectionsLoader();

        // Load all test entries across all suites
        var allTests = LoadAllTests();

        var selectionInfos = selections.Select(kvp =>
        {
            var name = kvp.Key;
            var config = kvp.Value;

            var matched = ApplyFilters(allTests, config);
            var totalDuration = ComputeTotalDuration(matched);

            return new
            {
                name,
                description = config.Description,
                filters = new
                {
                    tags = config.Tags,
                    priorities = config.Priorities,
                    components = config.Components,
                    has_automation = config.HasAutomation
                },
                estimated_test_count = matched.Count,
                estimated_duration = FindTestCasesTool.FormatDuration(totalDuration)
            };
        }).ToList();

        var data = new { selections = selectionInfos };

        return Task.FromResult(JsonSerializer.Serialize(McpToolResponse<object>.Success(data)));
    }

    private List<TestIndexEntry> LoadAllTests()
    {
        var allTests = new List<TestIndexEntry>();
        foreach (var suite in _suiteListLoader())
        {
            try
            {
                allTests.AddRange(_indexLoader(suite));
            }
            catch
            {
                // Skip malformed suites
            }
        }
        return allTests;
    }

    internal static List<TestIndexEntry> ApplyFilters(List<TestIndexEntry> tests, SavedSelectionConfig config)
    {
        var filtered = tests.AsEnumerable();

        if (config.Priorities is { Count: > 0 })
        {
            var priorities = new HashSet<string>(config.Priorities, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => priorities.Contains(t.Priority));
        }

        if (config.Tags is { Count: > 0 })
        {
            var tags = new HashSet<string>(config.Tags, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => t.Tags.Any(tag => tags.Contains(tag)));
        }

        if (config.Components is { Count: > 0 })
        {
            var components = new HashSet<string>(config.Components, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => t.Component is not null && components.Contains(t.Component));
        }

        if (config.HasAutomation is not null)
        {
            filtered = config.HasAutomation.Value
                ? filtered.Where(t => t.AutomatedBy.Count > 0)
                : filtered.Where(t => t.AutomatedBy.Count == 0);
        }

        return filtered.ToList();
    }

    private static TimeSpan ComputeTotalDuration(List<TestIndexEntry> entries)
    {
        var total = TimeSpan.Zero;
        foreach (var entry in entries)
        {
            if (FindTestCasesTool.TryParseDuration(entry.EstimatedDuration, out var d))
                total += d;
        }
        return total;
    }
}
