using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Represents the _index.json metadata for a suite.
/// </summary>
public sealed class MetadataIndex
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("generated_at")]
    public required DateTime GeneratedAt { get; init; }

    [JsonPropertyName("test_count")]
    public int TestCount => Tests.Count;

    [JsonPropertyName("tests")]
    public required IReadOnlyList<TestIndexEntry> Tests { get; init; }
}
