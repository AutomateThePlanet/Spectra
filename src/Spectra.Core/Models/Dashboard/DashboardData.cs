using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Root data structure containing all information needed to render the dashboard.
/// </summary>
public sealed record DashboardData
{
    /// <summary>Schema version for client compatibility checking.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    /// <summary>When the dashboard was generated (UTC).</summary>
    [JsonPropertyName("generated_at")]
    public required DateTime GeneratedAt { get; init; }

    /// <summary>Repository name or path.</summary>
    [JsonPropertyName("repository")]
    public required string Repository { get; init; }

    /// <summary>Statistics for each test suite.</summary>
    [JsonPropertyName("suites")]
    public IReadOnlyList<SuiteStats> Suites { get; init; } = [];

    /// <summary>Execution run history.</summary>
    [JsonPropertyName("runs")]
    public IReadOnlyList<RunSummary> Runs { get; init; } = [];

    /// <summary>All test entries (denormalized for client-side filtering).</summary>
    [JsonPropertyName("tests")]
    public IReadOnlyList<TestEntry> Tests { get; init; } = [];

    /// <summary>Coverage visualization data.</summary>
    [JsonPropertyName("coverage")]
    public CoverageData? Coverage { get; init; }

    /// <summary>Trend data for pass rate over time.</summary>
    [JsonPropertyName("trends")]
    public TrendData? Trends { get; init; }
}
