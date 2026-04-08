using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class AnalyzeCoverageResult : CommandResult
{
    [JsonPropertyName("documentation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CoverageSection? Documentation { get; init; }

    [JsonPropertyName("acceptanceCriteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CoverageSection? AcceptanceCriteria { get; init; }

    [JsonPropertyName("automation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutomationSection? Automation { get; init; }

    [JsonPropertyName("undocumented_tests")]
    public int UndocumentedTests { get; init; }

    [JsonPropertyName("uncovered_areas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UncoveredArea>? UncoveredAreas { get; init; }
}

public sealed class CoverageSection
{
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }

    [JsonPropertyName("covered")]
    public int Covered { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

public sealed class AutomationSection
{
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }

    [JsonPropertyName("linked")]
    public int Linked { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

public sealed class UncoveredArea
{
    [JsonPropertyName("doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Doc { get; init; }

    [JsonPropertyName("requirement")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Requirement { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
