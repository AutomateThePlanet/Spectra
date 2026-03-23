using System.Text.Json.Serialization;
using Spectra.Core.Models;

namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// A single testable behavior identified by AI analysis of source documentation.
/// </summary>
public sealed record IdentifiedBehavior
{
    /// <summary>
    /// Which category this behavior belongs to.
    /// </summary>
    [JsonPropertyName("category")]
    public required string CategoryRaw { get; init; }

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

    /// <summary>
    /// Parsed category enum value.
    /// </summary>
    [JsonIgnore]
    public BehaviorCategory Category => ParseCategory(CategoryRaw);

    private static BehaviorCategory ParseCategory(string raw) => raw?.ToLowerInvariant() switch
    {
        "happy_path" or "happypath" => BehaviorCategory.HappyPath,
        "negative" or "error" => BehaviorCategory.Negative,
        "edge_case" or "edgecase" or "boundary" => BehaviorCategory.EdgeCase,
        "security" or "permission" => BehaviorCategory.Security,
        "performance" or "load" => BehaviorCategory.Performance,
        _ => BehaviorCategory.HappyPath
    };
}
