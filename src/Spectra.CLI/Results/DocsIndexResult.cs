using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class DocsIndexResult : CommandResult
{
    // ── Legacy single-file fields (kept for backward-compat readers) ──

    [JsonPropertyName("documents_indexed")]
    public int DocumentsIndexed { get; init; }

    [JsonPropertyName("documents_updated")]
    public int DocumentsUpdated { get; init; }

    [JsonPropertyName("documents_skipped")]
    public int DocumentsSkipped { get; init; }

    [JsonPropertyName("documents_new")]
    public int DocumentsNew { get; init; }

    [JsonPropertyName("documents_total")]
    public int DocumentsTotal { get; init; }

    [JsonPropertyName("index_path")]
    public required string IndexPath { get; init; }

    // ── v2 layout fields (Spec 040) ──

    /// <summary>
    /// Per-suite breakdown for the v2 layout. Null on v1-only invocations.
    /// </summary>
    [JsonPropertyName("suites")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SuiteResultEntry>? Suites { get; init; }

    /// <summary>
    /// Repo-relative path to <c>_manifest.yaml</c>. Null on v1-only invocations.
    /// </summary>
    [JsonPropertyName("manifest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Manifest { get; init; }

    /// <summary>
    /// Migration metadata. Present only when a one-time legacy → v2 migration
    /// ran during this invocation.
    /// </summary>
    [JsonPropertyName("migration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MigrationRecord? Migration { get; init; }

    // ── Pre-existing criteria fields ──

    [JsonPropertyName("criteria_extracted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CriteriaExtracted { get; init; }

    [JsonPropertyName("criteria_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CriteriaFile { get; init; }
}

/// <summary>
/// Per-suite entry surfaced in the JSON result.
/// </summary>
public sealed class SuiteResultEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("document_count")]
    public required int DocumentCount { get; init; }

    [JsonPropertyName("tokens_estimated")]
    public required int TokensEstimated { get; init; }

    [JsonPropertyName("skip_analysis")]
    public required bool SkipAnalysis { get; init; }

    [JsonPropertyName("excluded_by")]
    public string ExcludedBy { get; init; } = "none";

    [JsonPropertyName("excluded_pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExcludedPattern { get; init; }

    [JsonPropertyName("index_file")]
    public required string IndexFile { get; init; }
}
