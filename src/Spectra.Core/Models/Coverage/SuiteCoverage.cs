using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Coverage statistics for a single test suite.
/// </summary>
public sealed class SuiteCoverage
{
    /// <summary>Suite name.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Total tests in suite.</summary>
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    /// <summary>Automated tests in suite.</summary>
    [JsonPropertyName("automated")]
    public required int Automated { get; init; }

    /// <summary>Coverage percentage (automated / total * 100).</summary>
    [JsonPropertyName("coverage_percentage")]
    public required decimal CoveragePercentage { get; init; }
}
