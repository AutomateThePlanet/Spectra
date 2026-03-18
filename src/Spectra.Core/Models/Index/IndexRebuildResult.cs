using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Index;

/// <summary>
/// Result of rebuilding index files for test suites.
/// </summary>
public sealed record IndexRebuildResult
{
    /// <summary>
    /// Number of suites processed.
    /// </summary>
    [JsonPropertyName("suites_processed")]
    public int SuitesProcessed { get; init; }

    /// <summary>
    /// Total tests added to indexes.
    /// </summary>
    [JsonPropertyName("tests_indexed")]
    public int TestsIndexed { get; init; }

    /// <summary>
    /// Number of new test files discovered.
    /// </summary>
    [JsonPropertyName("files_added")]
    public int FilesAdded { get; init; }

    /// <summary>
    /// Number of orphaned entries removed.
    /// </summary>
    [JsonPropertyName("files_removed")]
    public int FilesRemoved { get; init; }

    /// <summary>
    /// Paths to updated index files.
    /// </summary>
    [JsonPropertyName("index_paths")]
    public IReadOnlyList<string> IndexPaths { get; init; } = [];
}
