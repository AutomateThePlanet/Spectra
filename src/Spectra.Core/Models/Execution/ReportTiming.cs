using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Run-level timing breakdown derived from the per-test results in a report.
/// </summary>
public sealed record ReportTiming
{
    /// <summary>Sum of duration across results that recorded a duration, in milliseconds.</summary>
    [JsonPropertyName("total_test_duration_ms")]
    public required long TotalTestDurationMs { get; init; }

    /// <summary>Average duration per executed test (with a recorded duration), in milliseconds.</summary>
    [JsonPropertyName("average_test_duration_ms")]
    public required long AverageTestDurationMs { get; init; }
}
