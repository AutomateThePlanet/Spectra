using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Lightweight map of all documentation files for AI context selection.
/// </summary>
public sealed class DocumentMap
{
    [JsonPropertyName("doc_count")]
    public int DocCount => Documents.Count;

    [JsonPropertyName("total_size_kb")]
    public required int TotalSizeKb { get; init; }

    [JsonPropertyName("documents")]
    public required IReadOnlyList<DocumentEntry> Documents { get; init; }
}
