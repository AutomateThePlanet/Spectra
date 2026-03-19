using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Represents a source documentation file with full content for AI generation.
/// Unlike DocumentEntry (which only has a preview), this contains the full text.
/// </summary>
public sealed class SourceDocument
{
    /// <summary>
    /// Relative path to the document from the docs directory.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Document title extracted from the first heading.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Full document content (Markdown or plain text).
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Section headings extracted from the document for reference.
    /// </summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<string> Sections { get; init; } = [];

    /// <summary>
    /// File size in kilobytes.
    /// </summary>
    [JsonPropertyName("size_kb")]
    public int SizeKb { get; init; }
}
