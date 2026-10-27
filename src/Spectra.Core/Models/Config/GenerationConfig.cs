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

    /// <summary>The default of <c>duplicate_threshold</c> and <c>coverage_threshold</c>.</summary>
    public const double DefaultThreshold = 0.6;

    /// <summary>
    /// <c>duplicate_threshold</c> as written (null when missing): the duplicate-detection threshold. Spec 098 (F9): kept
    /// nullable so "set" is known for the one-release coverage fallback.
    /// </summary>
    [JsonPropertyName("duplicate_threshold")]
    public double? DuplicateThresholdSetting { get; init; }

    /// <summary>The duplicate-detection threshold (<c>duplicate_threshold</c>, default 0.6).</summary>
    [JsonIgnore]
    public double DuplicateThreshold => DuplicateThresholdSetting ?? DefaultThreshold;

    /// <summary>
    /// Spec 098 (F9, FR-019): the per-behaviour coverage threshold — a behaviour is already covered when a suite test's
    /// title is at least this similar. Optional; missing or <c>null</c> means 0.6. Must be 0–1.
    /// </summary>
    [JsonPropertyName("coverage_threshold")]
    public double? CoverageThreshold { get; init; }

    /// <summary>
    /// Spec 098 (F9, FR-019, FR-020): the coverage threshold and the config key it came from, with a notice when it fell
    /// back to <c>duplicate_threshold</c>. The fallback applies for one release, only when <c>coverage_threshold</c> is
    /// absent and <c>duplicate_threshold</c> was set to something other than its default (the init template writes 0.6,
    /// which equals the coverage default, so it needs no notice).
    /// </summary>
    public (double Value, string Key, string? Notice) CoverageThresholdChoice()
    {
        if (CoverageThreshold is { } coverage)
        {
            return (coverage, "generation.coverage_threshold", null);
        }

        if (DuplicateThresholdSetting is { } duplicate && duplicate != DefaultThreshold)
        {
            var shown = duplicate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return (duplicate, "generation.duplicate_threshold",
                $"generation.coverage_threshold is not set; using generation.duplicate_threshold ({shown}) for coverage as before. "
                + "Set generation.coverage_threshold — this fallback will be removed in the next release.");
        }

        return (DefaultThreshold, "generation.coverage_threshold", null);
    }

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
