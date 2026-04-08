using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ListCriteriaResult : CommandResult
{
    [JsonPropertyName("criteria")]
    public required IReadOnlyList<ListCriterionEntry> Criteria { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("covered")]
    public int Covered { get; init; }

    [JsonPropertyName("coverage_pct")]
    public decimal CoveragePct { get; init; }
}

public sealed class ListCriterionEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("rfc2119")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rfc2119 { get; init; }

    [JsonPropertyName("source_type")]
    public required string SourceType { get; init; }

    [JsonPropertyName("source_doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceDoc { get; init; }

    [JsonPropertyName("component")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Component { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }

    [JsonPropertyName("linked_tests")]
    public IReadOnlyList<string> LinkedTests { get; init; } = [];

    [JsonPropertyName("covered")]
    public bool Covered { get; init; }
}
