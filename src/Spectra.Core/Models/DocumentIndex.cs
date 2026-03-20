using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Top-level container for the documentation index.
/// </summary>
public sealed class DocumentIndex
{
    [JsonPropertyName("generated_at")]
    public required DateTimeOffset GeneratedAt { get; init; }

    [JsonPropertyName("total_documents")]
    public int TotalDocuments => Entries.Count;

    [JsonPropertyName("total_word_count")]
    public required int TotalWordCount { get; init; }

    [JsonPropertyName("total_estimated_tokens")]
    public required int TotalEstimatedTokens { get; init; }

    [JsonPropertyName("entries")]
    public required IReadOnlyList<DocumentIndexEntry> Entries { get; init; }
}
