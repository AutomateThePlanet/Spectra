using System.Text.Json.Serialization;

namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// A single testable behavior identified by AI analysis of source documentation.
/// </summary>
public sealed record IdentifiedBehavior
{
    /// <summary>
    /// Which category this behavior belongs to. Free-form string identifier
    /// returned verbatim from the AI (e.g., "happy_path", "keyboard_interaction").
    /// </summary>
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>
    /// Short description of the testable behavior (max 80 chars).
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Source document path this behavior was identified from.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }
}
