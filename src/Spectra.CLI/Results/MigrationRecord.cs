using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Metadata produced by a one-time legacy → v2 documentation-index migration
/// (Spec 040 §3.8). Surfaced in <see cref="DocsIndexResult.Migration"/> when
/// migration ran during the invocation.
/// </summary>
public sealed class MigrationRecord
{
    /// <summary>
    /// True when migration ran during this invocation. False on subsequent runs
    /// after a successful migration (when the legacy file is already gone).
    /// </summary>
    [JsonPropertyName("performed")]
    public required bool Performed { get; init; }

    /// <summary>
    /// Repo-relative path to <c>_index.md.bak</c> on success; null when no
    /// migration occurred or when the migration failed before backup.
    /// </summary>
    [JsonPropertyName("legacy_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyFile { get; init; }

    [JsonPropertyName("suites_created")]
    public int SuitesCreated { get; init; }

    [JsonPropertyName("documents_migrated")]
    public int DocumentsMigrated { get; init; }

    /// <summary>
    /// Suite ID with the largest <c>tokens_estimated</c>. Null when no suites
    /// were created (empty corpus).
    /// </summary>
    [JsonPropertyName("largest_suite_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LargestSuiteId { get; init; }

    [JsonPropertyName("largest_suite_tokens")]
    public int LargestSuiteTokens { get; init; }

    /// <summary>
    /// Non-blocking warnings (e.g., "12 entries had no checksum and were
    /// re-hashed from disk"). Empty on a clean migration.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
