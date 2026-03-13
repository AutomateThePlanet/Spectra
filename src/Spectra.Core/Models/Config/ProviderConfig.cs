using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for an AI provider.
/// </summary>
public sealed class ProviderConfig
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("priority")]
    public int Priority { get; init; } = 1;

    [JsonPropertyName("api_key_env")]
    public string? ApiKeyEnv { get; init; }

    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }
}
