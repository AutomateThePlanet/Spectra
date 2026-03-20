using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Per-section detail extracted from a documentation file.
/// </summary>
public sealed class SectionSummary
{
    [JsonPropertyName("heading")]
    public required string Heading { get; init; }

    [JsonPropertyName("level")]
    public required int Level { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }
}
