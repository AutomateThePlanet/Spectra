using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

public sealed class AcceptanceCriteriaCoverage
{
    [JsonPropertyName("total_criteria")]
    public required int TotalCriteria { get; init; }

    [JsonPropertyName("covered_criteria")]
    public required int CoveredCriteria { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("has_criteria_file")]
    public required bool HasCriteriaFile { get; init; }

    [JsonPropertyName("details")]
    public IReadOnlyList<CriteriaCoverageDetail> Details { get; init; } = [];

    [JsonPropertyName("source_breakdown")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, SourceCoverageStats>? SourceBreakdown { get; init; }
}

public sealed class CriteriaCoverageDetail
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("tests")]
    public IReadOnlyList<string> Tests { get; init; } = [];

    [JsonPropertyName("covered")]
    public required bool Covered { get; init; }
}

public sealed class SourceCoverageStats
{
    [JsonPropertyName("source_type")]
    public required string SourceType { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("covered")]
    public required int Covered { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }
}
