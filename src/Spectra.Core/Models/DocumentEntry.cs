using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Entry in the document map for a single documentation file.
/// </summary>
public sealed class DocumentEntry
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("size_kb")]
    public required int SizeKb { get; init; }

    [JsonPropertyName("headings")]
    public required IReadOnlyList<string> Headings { get; init; }

    [JsonPropertyName("first_200_chars")]
    public required string Preview { get; init; }
}
