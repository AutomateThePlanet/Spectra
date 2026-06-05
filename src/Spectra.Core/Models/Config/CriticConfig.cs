using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for the grounding verification critic.
/// </summary>
public sealed class CriticConfig
{
    /// <summary>
    /// Whether critic verification is enabled.
    /// Default: false (backward compatible).
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Critic model selector. Spec 058: this is the single surviving model selector in the whole
    /// config — the retired <c>provider</c>/<c>api_key_env</c>/<c>base_url</c> keys are gone. When
    /// unset, <c>CriticModelResolver</c> applies the same-family default. The Claude Code session
    /// supplies the runtime, so no provider/credential selection lives here.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Timeout for each critic verification call in seconds.
    /// Default: 120 seconds (was 30 prior to v1.43.0, but the actual runtime
    /// behavior was hardcoded to 2 minutes — the 30-second default was a
    /// dead value that the code ignored). v1.43.0 honors this field, with
    /// the default raised to 120 to preserve the prior behavior. Slow critic
    /// models on long tests may need 180–300 seconds.
    /// </summary>
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Maximum number of concurrent critic verification calls (Spec 043).
    /// Default: 1 (sequential — backward compatible with pre-1.48 behavior).
    /// Values are clamped to the inclusive range [1, 20] via
    /// <see cref="GetEffectiveMaxConcurrent"/>. Values greater than 10 risk
    /// provider rate limits — a warning is emitted at run start in that case.
    /// </summary>
    [JsonPropertyName("max_concurrent")]
    public int MaxConcurrent { get; init; } = 1;

    /// <summary>
    /// Returns the clamped <see cref="MaxConcurrent"/> value, pinned to the
    /// inclusive range [1, 20]. Values ≤0 clamp silently to 1; values &gt;20
    /// clamp to 20 (caller is responsible for emitting any stderr warning).
    /// </summary>
    public int GetEffectiveMaxConcurrent()
    {
        if (MaxConcurrent < 1) return 1;
        if (MaxConcurrent > 20) return 20;
        return MaxConcurrent;
    }

    /// <summary>
    /// Validates the configuration. Spec 058: the critic no longer has a provider — the Claude Code
    /// session supplies the runtime and <c>ai.critic.model</c> (optionally) names the model — so an
    /// enabled critic is always valid. (Retained for callers that previously gated on provider
    /// presence; it now only fails if a future required field is missing.)
    /// </summary>
    public bool IsValid() => true;
}
