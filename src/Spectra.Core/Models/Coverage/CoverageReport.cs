using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Output of coverage analysis command.
/// </summary>
public sealed class CoverageReport
{
    /// <summary>When the analysis was run (UTC).</summary>
    [JsonPropertyName("generated_at")]
    public required DateTime GeneratedAt { get; init; }

    /// <summary>Aggregate statistics.</summary>
    [JsonPropertyName("summary")]
    public required CoverageSummary Summary { get; init; }

    /// <summary>Coverage statistics per suite.</summary>
    [JsonPropertyName("by_suite")]
    public IReadOnlyList<SuiteCoverage> BySuite { get; init; } = [];

    /// <summary>Coverage statistics per component.</summary>
    [JsonPropertyName("by_component")]
    public IReadOnlyList<ComponentCoverage> ByComponent { get; init; } = [];

    /// <summary>Tests without automation links.</summary>
    [JsonPropertyName("unlinked_tests")]
    public IReadOnlyList<UnlinkedTest> UnlinkedTests { get; init; } = [];

    /// <summary>Automation files referencing non-existent tests.</summary>
    [JsonPropertyName("orphaned_automation")]
    public IReadOnlyList<OrphanedAutomation> OrphanedAutomation { get; init; } = [];

    /// <summary>References to non-existent files.</summary>
    [JsonPropertyName("broken_links")]
    public IReadOnlyList<BrokenLink> BrokenLinks { get; init; } = [];

    /// <summary>Inconsistent bidirectional links.</summary>
    [JsonPropertyName("mismatches")]
    public IReadOnlyList<LinkMismatch> Mismatches { get; init; } = [];
}
