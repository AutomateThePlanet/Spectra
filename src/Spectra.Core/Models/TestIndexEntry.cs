using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Entry in the _index.json metadata file.
/// </summary>
public sealed class TestIndexEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("component")]
    public string? Component { get; init; }

    [JsonPropertyName("depends_on")]
    public string? DependsOn { get; init; }

    [JsonPropertyName("source_refs")]
    public IReadOnlyList<string> SourceRefs { get; init; } = [];

    /// <summary>
    /// ID of the test this one duplicates (if redundant).
    /// </summary>
    [JsonPropertyName("redundant_of")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedundantOf { get; init; }

    /// <summary>
    /// Reason this test was flagged as redundant.
    /// </summary>
    [JsonPropertyName("redundant_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedundantReason { get; init; }
}
