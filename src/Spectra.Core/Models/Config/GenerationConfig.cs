using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test generation.
/// </summary>
public sealed class GenerationConfig
{
    [JsonPropertyName("default_count")]
    public int DefaultCount { get; init; } = 15;

    [JsonPropertyName("require_review")]
    public bool RequireReview { get; init; } = true;

    [JsonPropertyName("duplicate_threshold")]
    public double DuplicateThreshold { get; init; } = 0.6;

    [JsonPropertyName("categories")]
    public IReadOnlyList<string> Categories { get; init; } =
        ["happy_path", "negative", "boundary", "integration"];
}
