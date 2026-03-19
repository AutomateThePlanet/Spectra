using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Summary of an execution run for history display.
/// </summary>
public sealed class RunSummary
{
    /// <summary>Unique run identifier (UUID).</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Suite that was executed.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Run status (completed/cancelled/abandoned).</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>When the run started (UTC).</summary>
    [JsonPropertyName("started_at")]
    public required DateTime StartedAt { get; init; }

    /// <summary>When the run completed (UTC), null if still running.</summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; init; }

    /// <summary>User who started the run.</summary>
    [JsonPropertyName("started_by")]
    public required string StartedBy { get; init; }

    /// <summary>Total duration in seconds.</summary>
    [JsonPropertyName("duration_seconds")]
    public int? DurationSeconds { get; init; }

    /// <summary>Total tests in this run.</summary>
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    /// <summary>Tests that passed.</summary>
    [JsonPropertyName("passed")]
    public required int Passed { get; init; }

    /// <summary>Tests that failed.</summary>
    [JsonPropertyName("failed")]
    public required int Failed { get; init; }

    /// <summary>Tests that were skipped.</summary>
    [JsonPropertyName("skipped")]
    public required int Skipped { get; init; }

    /// <summary>Tests that were blocked.</summary>
    [JsonPropertyName("blocked")]
    public required int Blocked { get; init; }

    /// <summary>Individual test results for this run.</summary>
    [JsonPropertyName("results")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TestResultEntry>? Results { get; init; }
}
