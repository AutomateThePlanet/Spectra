using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test updates.
/// </summary>
public sealed class UpdateConfig
{
    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; init; } = 30;

    [JsonPropertyName("require_review")]
    public bool RequireReview { get; init; } = true;

    /// <summary>
    /// Spec 084: tests per <c>update-triage</c> group. Optional; missing or <c>null</c> means 10. Must be ≥ 1.
    /// </summary>
    [JsonPropertyName("group_size")]
    public int? GroupSize { get; init; }

    /// <summary>
    /// Spec 084: a changed section with more changed words than this is LOAD rather than LIGHT. Optional; missing or
    /// <c>null</c> means 12. Must be ≥ 0.
    /// </summary>
    [JsonPropertyName("light_threshold_words")]
    public int? LightThresholdWords { get; init; }

    [JsonIgnore]
    public int EffectiveGroupSize => GroupSize ?? 10;

    [JsonIgnore]
    public int EffectiveLightThresholdWords => LightThresholdWords ?? 12;
}
