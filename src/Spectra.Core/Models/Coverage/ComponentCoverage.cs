using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Coverage statistics for a component.
/// </summary>
public sealed class ComponentCoverage
{
    /// <summary>Component name.</summary>
    [JsonPropertyName("component")]
    public required string Component { get; init; }

    /// <summary>Total tests for this component.</summary>
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    /// <summary>Automated tests for this component.</summary>
    [JsonPropertyName("automated")]
    public required int Automated { get; init; }

    /// <summary>Coverage percentage (automated / total * 100).</summary>
    [JsonPropertyName("coverage_percentage")]
    public required decimal CoveragePercentage { get; init; }
}
