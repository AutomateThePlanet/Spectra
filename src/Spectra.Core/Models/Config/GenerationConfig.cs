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

    /// <summary>
    /// Spec 094: behaviours per generation batch in the analysis plan. Optional; missing or <c>null</c> means 8. Must be
    /// 1–50.
    /// </summary>
    [JsonPropertyName("batch_size")]
    public int? BatchSize { get; init; }

    [JsonIgnore]
    public int EffectiveBatchSize => BatchSize ?? 8;
}
