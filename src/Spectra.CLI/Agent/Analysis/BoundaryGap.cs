using System.Text.Json.Serialization;

namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// Spec 062: a single uncovered boundary condition surfaced by the in-session behavior
/// analysis — an edge the docs/criteria imply should be tested (min/max, off-by-one,
/// empty/null, overflow, timeout) but that the planned/existing tests do not cover.
///
/// Deserialized from one element of the top-level <c>boundary_gaps</c> array the agent emits
/// alongside <c>behaviors</c> (mirroring how <c>field_specs</c> rides alongside in
/// <see cref="BehaviorAnalysisResult"/>). Advisory — never persisted, never mutates the
/// generated tests, never blocks generation.
///
/// Validity is enforced fail-loud in <see cref="Spectra.CLI.Generation.AnalysisRecommendationBuilder"/>
/// (not via <c>required</c>/deserialization) so a malformed element yields a specific,
/// index-attributed error rather than a generic parse exception.
/// </summary>
public sealed record BoundaryGap
{
    /// <summary>The parameter/field or behavior the boundary concerns (e.g. "username", "order total").</summary>
    [JsonPropertyName("field")]
    public string Field { get; init; } = "";

    /// <summary>
    /// The boundary kind. Free-form (like <see cref="IdentifiedBehavior.Technique"/>) to avoid
    /// rejecting valid-but-novel kinds; the prompt steers toward the canonical vocabulary:
    /// <c>min-max</c>, <c>off-by-one</c>, <c>empty-null</c>, <c>overflow</c>, <c>timeout</c>, <c>max-length</c>.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    /// <summary>Short description of the missing edge (e.g. "21-char input (max 20) is untested").</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>
    /// Document/criterion that implies the boundary. Optional — empty is valid (the model may
    /// infer a boundary from combined context) and is NOT treated as malformed.
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = "";
}
