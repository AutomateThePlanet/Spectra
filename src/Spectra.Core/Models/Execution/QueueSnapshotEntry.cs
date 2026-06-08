using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// A durable, write-once-at-run-build capture of a single test's orchestration data
/// (title, priority, dependency edge, and original queue position). Persisted when a run
/// is created so the execution queue can be reconstructed losslessly from the database
/// alone — independent of the mutable on-disk test index. See spec 064.
/// </summary>
public sealed record QueueSnapshotEntry
{
    /// <summary>Owning run.</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Test case ID (e.g., TC-101).</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>Display title captured at run-build time.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Canonical priority string (high/medium/low) as captured from the index.</summary>
    [JsonPropertyName("priority")]
    public required string Priority { get; init; }

    /// <summary>ID of the test this depends on, if any.</summary>
    [JsonPropertyName("depends_on")]
    public string? DependsOn { get; init; }

    /// <summary>0-based position in the built queue's ordered list (priority-then-topological order).</summary>
    [JsonPropertyName("order_index")]
    public required int OrderIndex { get; init; }
}
