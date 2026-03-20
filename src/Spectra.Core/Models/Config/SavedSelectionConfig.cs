using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for a saved test selection.
/// </summary>
public sealed class SavedSelectionConfig
{
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonPropertyName("priorities")]
    public IReadOnlyList<string>? Priorities { get; init; }

    [JsonPropertyName("components")]
    public IReadOnlyList<string>? Components { get; init; }

    [JsonPropertyName("has_automation")]
    public bool? HasAutomation { get; init; }
}
