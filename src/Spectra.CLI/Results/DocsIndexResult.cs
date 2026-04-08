using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class DocsIndexResult : CommandResult
{
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

    [JsonPropertyName("criteria_extracted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CriteriaExtracted { get; init; }

    [JsonPropertyName("criteria_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CriteriaFile { get; init; }
}
