using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Automation coverage: how many tests have automation links.
/// </summary>
public sealed class AutomationCoverage
{
    [JsonPropertyName("total_tests")]
    public required int TotalTests { get; init; }

    [JsonPropertyName("automated")]
    public required int Automated { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("by_suite")]
    public IReadOnlyList<SuiteCoverage> BySuite { get; init; } = [];

    [JsonPropertyName("unlinked_tests")]
    public IReadOnlyList<UnlinkedTest> UnlinkedTests { get; init; } = [];

    [JsonPropertyName("orphaned_automation")]
    public IReadOnlyList<OrphanedAutomation> OrphanedAutomation { get; init; } = [];

    [JsonPropertyName("broken_links")]
    public IReadOnlyList<BrokenLink> BrokenLinks { get; init; } = [];
}
