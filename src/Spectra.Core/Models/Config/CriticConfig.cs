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
    /// Provider name: "google", "openai", "anthropic", "github".
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
    /// Timeout for verification calls in seconds.
    /// Default: 30 seconds.
    /// </summary>
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Gets the effective model name for the provider.
    /// </summary>
    public string GetEffectiveModel() => Model ?? Provider?.ToLowerInvariant() switch
    {
        "google" => "gemini-2.0-flash",
        "openai" => "gpt-4o-mini",
        "anthropic" => "claude-3-5-haiku-latest",
        "github" => "gpt-4o-mini",
        _ => "gpt-4o-mini"
    };

    /// <summary>
    /// Gets the default API key environment variable for the provider.
    /// </summary>
    public string GetDefaultApiKeyEnv() => Provider?.ToLowerInvariant() switch
    {
        "google" => "GOOGLE_API_KEY",
        "openai" => "OPENAI_API_KEY",
        "anthropic" => "ANTHROPIC_API_KEY",
        "github" => "GITHUB_TOKEN",
        _ => "OPENAI_API_KEY"
    };

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid() =>
        !Enabled || !string.IsNullOrWhiteSpace(Provider);
}
