using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Aggregate coverage statistics.
/// </summary>
public sealed class CoverageSummary
{
    /// <summary>Total manual tests.</summary>
    [JsonPropertyName("total_tests")]
    public required int TotalTests { get; init; }

    /// <summary>Tests with automation links.</summary>
    [JsonPropertyName("automated")]
    public required int Automated { get; init; }

    /// <summary>Tests without automation.</summary>
    [JsonPropertyName("manual_only")]
    public required int ManualOnly { get; init; }

    /// <summary>Coverage percentage (automated / total * 100).</summary>
    [JsonPropertyName("coverage_percentage")]
    public required decimal CoveragePercentage { get; init; }

    /// <summary>Count of broken links.</summary>
    [JsonPropertyName("broken_links")]
    public int BrokenLinks { get; init; }

    /// <summary>Count of orphaned automation files.</summary>
    [JsonPropertyName("orphaned_automation")]
    public int OrphanedAutomation { get; init; }

    /// <summary>Count of link mismatches.</summary>
    [JsonPropertyName("mismatches")]
    public int Mismatches { get; init; }
}
