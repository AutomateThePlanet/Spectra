using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Rich metadata per document in the documentation index.
/// </summary>
public sealed class DocumentIndexEntry
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("sections")]
    public required IReadOnlyList<SectionSummary> Sections { get; init; }

    [JsonPropertyName("key_entities")]
    public required IReadOnlyList<string> KeyEntities { get; init; }

    [JsonPropertyName("word_count")]
    public required int WordCount { get; init; }

    [JsonPropertyName("estimated_tokens")]
    public required int EstimatedTokens { get; init; }

    [JsonPropertyName("size_kb")]
    public required int SizeKb { get; init; }

    [JsonPropertyName("last_modified")]
    public required DateTimeOffset LastModified { get; init; }

    [JsonPropertyName("content_hash")]
    public required string ContentHash { get; init; }
}
