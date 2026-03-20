using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Three-section coverage summary for dashboard display.
/// </summary>
public sealed class CoverageSummaryData
{
    [JsonPropertyName("documentation")]
    public required CoverageSectionData Documentation { get; init; }

    [JsonPropertyName("requirements")]
    public required CoverageSectionData Requirements { get; init; }

    [JsonPropertyName("automation")]
    public required CoverageSectionData Automation { get; init; }
}

/// <summary>
/// A single coverage section with covered/total/percentage.
/// </summary>
public sealed class CoverageSectionData
{
    [JsonPropertyName("covered")]
    public required int Covered { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object>? Details { get; init; }
}
