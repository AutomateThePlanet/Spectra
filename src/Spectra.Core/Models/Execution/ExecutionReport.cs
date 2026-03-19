using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Complete execution report for a run.
/// </summary>
public sealed record ExecutionReport
{
    /// <summary>Run identifier.</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Test suite name.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Target environment.</summary>
    [JsonPropertyName("environment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Environment { get; init; }

    /// <summary>Run start time.</summary>
    [JsonPropertyName("started_at")]
    public required DateTime StartedAt { get; init; }

    /// <summary>Run completion time.</summary>
    [JsonPropertyName("completed_at")]
    public required DateTime CompletedAt { get; init; }

    /// <summary>Duration in minutes (UTC-normalized).</summary>
    [JsonPropertyName("duration_minutes")]
    public double DurationMinutes
    {
        get
        {
            // Normalize both timestamps to UTC to avoid timezone issues
            var startUtc = StartedAt.ToUniversalTime();
            var endUtc = CompletedAt.ToUniversalTime();
            var duration = endUtc - startUtc;
            // Guard against negative durations
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            return Math.Round(duration.TotalMinutes, 1);
        }
    }

    /// <summary>User who executed the run.</summary>
    [JsonPropertyName("executed_by")]
    public required string ExecutedBy { get; init; }

    /// <summary>Final run status.</summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RunStatus Status { get; init; }

    /// <summary>Aggregate test counts.</summary>
    [JsonPropertyName("summary")]
    public required ReportSummary Summary { get; init; }

    /// <summary>Individual test results.</summary>
    [JsonPropertyName("results")]
    public required IReadOnlyList<TestResultEntry> Results { get; init; }

    /// <summary>Applied filters.</summary>
    [JsonPropertyName("filters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunFilters? Filters { get; init; }
}
