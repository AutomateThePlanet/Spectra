using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Trend data for pass rate visualization over time.
/// </summary>
public sealed class TrendData
{
    /// <summary>Data points for the trend chart.</summary>
    [JsonPropertyName("points")]
    public IReadOnlyList<TrendPoint> Points { get; init; } = [];

    /// <summary>Overall pass rate across all runs.</summary>
    [JsonPropertyName("overall_pass_rate")]
    public decimal OverallPassRate { get; init; }

    /// <summary>Pass rate trend direction (improving, declining, stable).</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "stable";

    /// <summary>Aggregation by suite.</summary>
    [JsonPropertyName("by_suite")]
    public IReadOnlyList<SuiteTrend> BySuite { get; init; } = [];
}

/// <summary>
/// A single data point in the trend chart.
/// </summary>
public sealed class TrendPoint
{
    /// <summary>Date of the data point.</summary>
    [JsonPropertyName("date")]
    public DateTime Date { get; init; }

    /// <summary>Pass rate as percentage (0-100).</summary>
    [JsonPropertyName("pass_rate")]
    public decimal PassRate { get; init; }

    /// <summary>Total tests executed.</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>Tests passed.</summary>
    [JsonPropertyName("passed")]
    public int Passed { get; init; }

    /// <summary>Tests failed.</summary>
    [JsonPropertyName("failed")]
    public int Failed { get; init; }
}

/// <summary>
/// Trend data for a specific suite.
/// </summary>
public sealed class SuiteTrend
{
    /// <summary>Suite name.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Current pass rate.</summary>
    [JsonPropertyName("pass_rate")]
    public decimal PassRate { get; init; }

    /// <summary>Number of runs.</summary>
    [JsonPropertyName("run_count")]
    public int RunCount { get; init; }

    /// <summary>Pass rate change from previous period.</summary>
    [JsonPropertyName("change")]
    public decimal Change { get; init; }
}
