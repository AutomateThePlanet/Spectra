using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Index;

/// <summary>
/// Hash table mapping document paths to content checksums. Used by the
/// indexer for incremental-update detection. Lives in <c>_checksums.json</c>
/// alongside the manifest. <strong>NEVER sent as part of an AI prompt.</strong>
/// </summary>
public sealed class ChecksumStore
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// Keys: forward-slash repo-relative paths.
    /// Values: 64-character lowercase hex SHA-256 digests
    /// (matches <c>Spectra.Core.Parsing.DocumentIndexExtractor.ComputeHash</c>).
    /// </summary>
    [JsonPropertyName("checksums")]
    public Dictionary<string, string> Checksums { get; set; } = new();
}
