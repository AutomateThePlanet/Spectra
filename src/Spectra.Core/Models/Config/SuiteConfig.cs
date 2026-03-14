using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Per-suite configuration overrides.
/// </summary>
public sealed class SuiteConfig
{
    [JsonPropertyName("component")]
    public string? Component { get; init; }

    [JsonPropertyName("relevant_docs")]
    public IReadOnlyList<string> RelevantDocs { get; init; } = [];

    [JsonPropertyName("default_tags")]
    public IReadOnlyList<string> DefaultTags { get; init; } = [];

    [JsonPropertyName("default_priority")]
    public string DefaultPriority { get; init; } = "medium";
}
