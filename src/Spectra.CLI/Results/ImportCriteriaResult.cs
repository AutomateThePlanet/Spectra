using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ImportCriteriaResult : CommandResult
{
    [JsonPropertyName("imported")]
    public int Imported { get; init; }

    [JsonPropertyName("split")]
    public int Split { get; init; }

    [JsonPropertyName("normalized")]
    public int Normalized { get; init; }

    [JsonPropertyName("merged")]
    public int Merged { get; init; }

    [JsonPropertyName("new")]
    public int New { get; init; }

    [JsonPropertyName("total_criteria")]
    public int TotalCriteria { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("source_breakdown")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? SourceBreakdown { get; init; }
}
