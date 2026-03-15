using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Aggregated statistics for a test suite.
/// </summary>
public sealed class SuiteStats
{
    /// <summary>Suite name (folder name).</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Total number of tests in the suite.</summary>
    [JsonPropertyName("test_count")]
    public required int TestCount { get; init; }

    /// <summary>Count by priority level (high/medium/low).</summary>
    [JsonPropertyName("by_priority")]
    public IReadOnlyDictionary<string, int> ByPriority { get; init; } = new Dictionary<string, int>();

    /// <summary>Count by component.</summary>
    [JsonPropertyName("by_component")]
    public IReadOnlyDictionary<string, int> ByComponent { get; init; } = new Dictionary<string, int>();

    /// <summary>All unique tags in suite.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Most recent execution run for this suite.</summary>
    [JsonPropertyName("last_run")]
    public RunSummary? LastRun { get; init; }

    /// <summary>Percentage of tests with automation (0-100).</summary>
    [JsonPropertyName("automation_coverage")]
    public decimal AutomationCoverage { get; init; }
}
