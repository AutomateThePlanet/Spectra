using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Requirements coverage: how many requirements have linked tests.
/// </summary>
public sealed class RequirementsCoverage
{
    [JsonPropertyName("total_requirements")]
    public required int TotalRequirements { get; init; }

    [JsonPropertyName("covered_requirements")]
    public required int CoveredRequirements { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("has_requirements_file")]
    public required bool HasRequirementsFile { get; init; }

    [JsonPropertyName("details")]
    public IReadOnlyList<RequirementCoverageDetail> Details { get; init; } = [];
}

/// <summary>
/// Coverage detail for a single requirement.
/// </summary>
public sealed class RequirementCoverageDetail
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("tests")]
    public IReadOnlyList<string> Tests { get; init; } = [];

    [JsonPropertyName("covered")]
    public required bool Covered { get; init; }
}
