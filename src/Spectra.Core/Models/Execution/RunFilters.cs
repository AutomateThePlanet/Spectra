using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Filters applied when starting a test execution run.
/// </summary>
public sealed record RunFilters
{
    /// <summary>Filter by a single test priority (legacy nested shape).</summary>
    [JsonPropertyName("priority")]
    public Priority? Priority { get; init; }

    /// <summary>
    /// Filter by priorities (OR within array). Canonical top-level shape,
    /// matching find_test_cases. Raw strings — unknown values simply match
    /// nothing (Spec 051).
    /// </summary>
    [JsonPropertyName("priorities")]
    public IReadOnlyList<string>? Priorities { get; init; }

    /// <summary>Filter by tags (OR within array — Spec 051 unified semantics).</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Filter by a single component (legacy nested shape).</summary>
    [JsonPropertyName("component")]
    public string? Component { get; init; }

    /// <summary>Filter by components (OR within array). Canonical top-level shape.</summary>
    [JsonPropertyName("components")]
    public IReadOnlyList<string>? Components { get; init; }

    /// <summary>Filter by specific test IDs.</summary>
    [JsonPropertyName("test_ids")]
    public IReadOnlyList<string>? TestIds { get; init; }

    /// <summary>
    /// Returns true if any filter is applied.
    /// </summary>
    public bool HasFilters =>
        Priority.HasValue ||
        (Priorities?.Count > 0) ||
        (Tags?.Count > 0) ||
        !string.IsNullOrEmpty(Component) ||
        (Components?.Count > 0) ||
        (TestIds?.Count > 0);

    /// <summary>
    /// Builds a filter set from the canonical top-level plural arrays
    /// (matching find_test_cases). Empty/null arrays impose no constraint.
    /// </summary>
    public static RunFilters From(
        IReadOnlyList<string>? priorities,
        IReadOnlyList<string>? tags,
        IReadOnlyList<string>? components) => new()
    {
        Priorities = priorities,
        Tags = tags,
        Components = components
    };
}
