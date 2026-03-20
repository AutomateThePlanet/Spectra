using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.Data;

/// <summary>
/// MCP tool: find_test_cases
/// Cross-suite search and filter for test cases by metadata.
/// Pure deterministic C# — no AI calls.
/// </summary>
public sealed class FindTestCasesTool : IMcpTool
{
    private readonly Func<IEnumerable<string>> _suiteListLoader;
    private readonly Func<string, IEnumerable<TestIndexEntry>> _indexLoader;

    public string Description => "Search and filter test cases across all suites by metadata";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = "Free-text search (OR across keywords, case-insensitive, matches title + description + tags)" },
            suites = new { type = "array", items = new { type = "string" }, description = "Limit search to these suite names" },
            priorities = new { type = "array", items = new { type = "string" }, description = "Filter by priority (OR within array)" },
            tags = new { type = "array", items = new { type = "string" }, description = "Filter by tags (OR within array)" },
            components = new { type = "array", items = new { type = "string" }, description = "Filter by component (OR within array)" },
            has_automation = new { type = "boolean", description = "Filter by automation status" },
            max_results = new { type = "integer", description = "Maximum results to return (default: 50)" }
        }
    };

    public FindTestCasesTool(
        Func<IEnumerable<string>> suiteListLoader,
        Func<string, IEnumerable<TestIndexEntry>> indexLoader)
    {
        _suiteListLoader = suiteListLoader;
        _indexLoader = indexLoader;
    }

    public Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<FindTestCasesRequest>(parameters);
        var maxResults = request?.MaxResults ?? 50;
        if (maxResults < 1)
        {
            return Task.FromResult(JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "max_results must be at least 1")));
        }

        var warnings = new List<string>();

        // Determine which suites to search
        var suitesToSearch = request?.Suites is { Count: > 0 }
            ? request.Suites
            : _suiteListLoader().ToList();

        // Load all test entries across suites
        var allTests = new List<(string Suite, TestIndexEntry Entry)>();
        foreach (var suite in suitesToSearch)
        {
            try
            {
                var entries = _indexLoader(suite).ToList();
                if (entries.Count == 0)
                {
                    warnings.Add($"Skipped suite '{suite}' — _index.json not found or empty");
                    continue;
                }
                foreach (var entry in entries)
                {
                    allTests.Add((suite, entry));
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Skipped suite '{suite}' — {ex.Message}");
            }
        }

        // Apply filters (AND between types, OR within arrays)
        var filtered = allTests.AsEnumerable();

        if (request?.Priorities is { Count: > 0 })
        {
            var priorities = new HashSet<string>(request.Priorities, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => priorities.Contains(t.Entry.Priority));
        }

        if (request?.Tags is { Count: > 0 })
        {
            var tags = new HashSet<string>(request.Tags, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => t.Entry.Tags.Any(tag => tags.Contains(tag)));
        }

        if (request?.Components is { Count: > 0 })
        {
            var components = new HashSet<string>(request.Components, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => t.Entry.Component is not null && components.Contains(t.Entry.Component));
        }

        if (request?.HasAutomation is not null)
        {
            filtered = request.HasAutomation.Value
                ? filtered.Where(t => t.Entry.AutomatedBy.Count > 0)
                : filtered.Where(t => t.Entry.AutomatedBy.Count == 0);
        }

        var matchedList = filtered.ToList();

        // Apply free-text query scoring
        var keywords = ParseKeywords(request?.Query);
        List<(string Suite, TestIndexEntry Entry, int Score)> scored;

        if (keywords.Count > 0)
        {
            scored = matchedList
                .Select(t => (t.Suite, t.Entry, Score: ScoreKeywordHits(t.Entry, keywords)))
                .Where(t => t.Score > 0)
                .ToList();
        }
        else
        {
            scored = matchedList.Select(t => (t.Suite, t.Entry, Score: 0)).ToList();
        }

        var totalMatched = scored.Count;

        // Sort: keyword hits (if query) > priority descending > suite name > index order
        var sorted = keywords.Count > 0
            ? scored.OrderByDescending(t => t.Score)
                .ThenBy(t => PriorityOrder(t.Entry.Priority))
                .ThenBy(t => t.Suite)
                .ToList()
            : scored.OrderBy(t => PriorityOrder(t.Entry.Priority))
                .ThenBy(t => t.Suite)
                .ToList();

        // Compute total estimated duration from ALL matches
        var totalDuration = ComputeTotalDuration(sorted.Select(t => t.Entry));

        // Truncate to max_results
        var truncated = sorted.Take(maxResults).ToList();

        var data = new
        {
            matched = totalMatched,
            total_estimated_duration = FormatDuration(totalDuration),
            tests = truncated.Select(t => new
            {
                id = t.Entry.Id,
                suite = t.Suite,
                title = t.Entry.Title,
                description = t.Entry.Description,
                priority = t.Entry.Priority,
                tags = t.Entry.Tags,
                component = t.Entry.Component,
                estimated_duration = t.Entry.EstimatedDuration,
                has_automation = t.Entry.AutomatedBy.Count > 0
            }),
            warnings = warnings.Count > 0 ? warnings : null
        };

        return Task.FromResult(JsonSerializer.Serialize(McpToolResponse<object>.Success(data)));
    }

    private static List<string> ParseKeywords(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .ToList();
    }

    private static int ScoreKeywordHits(TestIndexEntry entry, List<string> keywords)
    {
        var searchText = string.Join(" ",
            entry.Title,
            entry.Description ?? "",
            string.Join(" ", entry.Tags)
        ).ToLowerInvariant();

        return keywords.Count(k => searchText.Contains(k));
    }

    private static int PriorityOrder(string priority) => priority.ToLowerInvariant() switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3
    };

    private static TimeSpan ComputeTotalDuration(IEnumerable<TestIndexEntry> entries)
    {
        var total = TimeSpan.Zero;
        foreach (var entry in entries)
        {
            if (TryParseDuration(entry.EstimatedDuration, out var duration))
            {
                total += duration;
            }
        }
        return total;
    }

    internal static bool TryParseDuration(string? value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (TimeSpan.TryParse(value, out duration)) return true;

        var match = System.Text.RegularExpressions.Regex.Match(value, @"^(?:(\d+)h)?(?:\s*(\d+)m)?(?:\s*(\d+)s)?$");
        if (!match.Success) return false;

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        duration = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero) return "0m";
        var parts = new List<string>();
        if (duration.Hours > 0 || duration.Days > 0)
            parts.Add($"{(int)duration.TotalHours}h");
        if (duration.Minutes > 0)
            parts.Add($"{duration.Minutes}m");
        if (parts.Count == 0 && duration.Seconds > 0)
            parts.Add($"{duration.Seconds}s");
        return string.Join(" ", parts);
    }
}

internal sealed class FindTestCasesRequest
{
    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("suites")]
    public List<string>? Suites { get; set; }

    [JsonPropertyName("priorities")]
    public List<string>? Priorities { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("components")]
    public List<string>? Components { get; set; }

    [JsonPropertyName("has_automation")]
    public bool? HasAutomation { get; set; }

    [JsonPropertyName("max_results")]
    public int? MaxResults { get; set; }
}
