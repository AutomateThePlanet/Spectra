using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Aggregate counts for an execution report.
/// </summary>
public sealed record ReportSummary
{
    /// <summary>Total number of tests in the run.</summary>
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    /// <summary>Number of passed tests.</summary>
    [JsonPropertyName("passed")]
    public required int Passed { get; init; }

    /// <summary>Number of failed tests.</summary>
    [JsonPropertyName("failed")]
    public required int Failed { get; init; }

    /// <summary>Number of skipped tests.</summary>
    [JsonPropertyName("skipped")]
    public required int Skipped { get; init; }

    /// <summary>Number of blocked tests.</summary>
    [JsonPropertyName("blocked")]
    public required int Blocked { get; init; }

    /// <summary>Number of pending tests (not executed).</summary>
    [JsonPropertyName("pending")]
    public int Pending => Total - Passed - Failed - Skipped - Blocked;

    /// <summary>Pass rate as a percentage (0-100).</summary>
    [JsonPropertyName("pass_rate")]
    public double PassRate => Total > 0 ? Math.Round(100.0 * Passed / Total, 1) : 0;

    /// <summary>
    /// Creates a summary from status counts.
    /// </summary>
    public static ReportSummary FromCounts(Dictionary<TestStatus, int> counts)
    {
        return new ReportSummary
        {
            Total = counts.Values.Sum(),
            Passed = counts.GetValueOrDefault(TestStatus.Passed),
            Failed = counts.GetValueOrDefault(TestStatus.Failed),
            Skipped = counts.GetValueOrDefault(TestStatus.Skipped),
            Blocked = counts.GetValueOrDefault(TestStatus.Blocked)
        };
    }
}
