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
    /// Provider name. Spec 039: the canonical set is identical to the
    /// generator provider list — "github-models", "azure-openai",
    /// "azure-anthropic", "openai", "anthropic". Legacy "github" is still
    /// recognized as a soft alias for "github-models" (with a deprecation
    /// warning); legacy "google" is rejected.
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// Model name (e.g., "gemini-2.0-flash", "gpt-4o-mini").
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Environment variable containing the API key.
    /// </summary>
    [JsonPropertyName("api_key_env")]
    public string? ApiKeyEnv { get; init; }

    /// <summary>
    /// Optional base URL override for the API.
    /// </summary>
    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }

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
    /// Gets the effective model name for the provider. Spec 041: defaults
    /// target current GitHub Copilot free / cross-architecture models —
    /// <c>gpt-5-mini</c> for GPT providers and <c>claude-haiku-4-5</c> for
    /// Claude providers. Legacy entries kept for read-side safety in case
    /// any caller bypasses CriticFactory.
    /// </summary>
    public string GetEffectiveModel() => Model ?? Provider?.ToLowerInvariant() switch
    {
        "github-models" => "gpt-5-mini",
        "azure-openai" => "gpt-5-mini",
        "azure-anthropic" => "claude-haiku-4-5",
        "openai" => "gpt-5-mini",
        "anthropic" => "claude-haiku-4-5",
        // Legacy fallthroughs (alias-resolved by CriticFactory before reaching here)
        "github" => "gpt-5-mini",
        "google" => "gemini-2.0-flash",
        _ => "gpt-5-mini"
    };

    /// <summary>
    /// Gets the default API key environment variable for the provider.
    /// </summary>
    public string GetDefaultApiKeyEnv() => Provider?.ToLowerInvariant() switch
    {
        "github-models" => "GITHUB_TOKEN",
        "azure-openai" => "AZURE_OPENAI_API_KEY",
        "azure-anthropic" => "AZURE_ANTHROPIC_API_KEY",
        "openai" => "OPENAI_API_KEY",
        "anthropic" => "ANTHROPIC_API_KEY",
        // Legacy fallthroughs
        "github" => "GITHUB_TOKEN",
        "google" => "GOOGLE_API_KEY",
        _ => "GITHUB_TOKEN"
    };

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid() =>
        !Enabled || !string.IsNullOrWhiteSpace(Provider);
}
