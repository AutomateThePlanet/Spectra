using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Per-test execution history statistics.
/// </summary>
public sealed class TestExecutionHistoryEntry
{
    [JsonPropertyName("last_executed")]
    public DateTime? LastExecuted { get; init; }

    [JsonPropertyName("last_status")]
    public string? LastStatus { get; init; }

    [JsonPropertyName("total_runs")]
    public int TotalRuns { get; init; }

    [JsonPropertyName("pass_rate")]
    public decimal? PassRate { get; init; }

    [JsonPropertyName("last_run_id")]
    public string? LastRunId { get; init; }
}
