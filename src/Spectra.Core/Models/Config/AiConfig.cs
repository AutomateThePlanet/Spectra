using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for AI providers.
/// </summary>
public sealed class AiConfig
{
    [JsonPropertyName("providers")]
    public required IReadOnlyList<ProviderConfig> Providers { get; init; }

    [JsonPropertyName("fallback_strategy")]
    public string FallbackStrategy { get; init; } = "auto";

    /// <summary>
    /// Configuration for the grounding verification critic.
    /// When enabled, generated tests are verified against source documentation.
    /// </summary>
    [JsonPropertyName("critic")]
    public CriticConfig? Critic { get; init; }
}
