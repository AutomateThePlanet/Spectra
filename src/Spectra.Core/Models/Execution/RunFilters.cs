using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Filters applied when starting a test execution run.
/// </summary>
public sealed record RunFilters
{
    /// <summary>Filter by test priority.</summary>
    [JsonPropertyName("priority")]
    public Priority? Priority { get; init; }

    /// <summary>Filter by tags (AND logic).</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Filter by component.</summary>
    [JsonPropertyName("component")]
    public string? Component { get; init; }

    /// <summary>Filter by specific test IDs.</summary>
    [JsonPropertyName("test_ids")]
    public IReadOnlyList<string>? TestIds { get; init; }

    /// <summary>
    /// Returns true if any filter is applied.
    /// </summary>
    public bool HasFilters =>
        Priority.HasValue ||
        (Tags?.Count > 0) ||
        !string.IsNullOrEmpty(Component) ||
        (TestIds?.Count > 0);
}
