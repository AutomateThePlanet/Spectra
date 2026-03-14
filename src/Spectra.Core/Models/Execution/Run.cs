using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Represents a single test execution session.
/// </summary>
public sealed class Run
{
    /// <summary>Unique identifier for the run (UUID).</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Name of the test suite being executed.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Current state of the run.</summary>
    [JsonPropertyName("status")]
    public required RunStatus Status { get; set; }

    /// <summary>UTC timestamp when run was created.</summary>
    [JsonPropertyName("started_at")]
    public required DateTime StartedAt { get; init; }

    /// <summary>User identity who started the run.</summary>
    [JsonPropertyName("started_by")]
    public required string StartedBy { get; init; }

    /// <summary>Target environment (staging, uat, prod).</summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    /// <summary>Applied test filters.</summary>
    [JsonPropertyName("filters")]
    public RunFilters? Filters { get; init; }

    /// <summary>UTC timestamp of last state change.</summary>
    [JsonPropertyName("updated_at")]
    public required DateTime UpdatedAt { get; set; }

    /// <summary>UTC timestamp when run was finalized.</summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
