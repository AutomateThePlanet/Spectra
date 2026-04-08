using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ExtractCriteriaResult : CommandResult
{
    [JsonPropertyName("documents_processed")]
    public int DocumentsProcessed { get; init; }

    [JsonPropertyName("documents_skipped")]
    public int DocumentsSkipped { get; init; }

    [JsonPropertyName("documents_failed")]
    public int DocumentsFailed { get; init; }

    [JsonPropertyName("failed_documents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? FailedDocuments { get; init; }

    [JsonPropertyName("criteria_extracted")]
    public int CriteriaExtracted { get; init; }

    [JsonPropertyName("criteria_new")]
    public int CriteriaNew { get; init; }

    [JsonPropertyName("criteria_updated")]
    public int CriteriaUpdated { get; init; }

    [JsonPropertyName("criteria_unchanged")]
    public int CriteriaUnchanged { get; init; }

    [JsonPropertyName("orphaned_criteria")]
    public int OrphanedCriteria { get; init; }

    [JsonPropertyName("total_criteria")]
    public int TotalCriteria { get; init; }

    [JsonPropertyName("index_file")]
    public required string IndexFile { get; init; }

    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CriterionEntry>? Criteria { get; init; }
}

public sealed class CriterionEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("rfc2119")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rfc2119 { get; init; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    [JsonPropertyName("source_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceType { get; init; }

    [JsonPropertyName("component")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Component { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }
}
