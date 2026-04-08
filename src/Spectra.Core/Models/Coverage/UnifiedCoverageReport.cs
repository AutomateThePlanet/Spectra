using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Unified coverage report combining documentation, requirements, and automation coverage.
/// </summary>
public sealed class UnifiedCoverageReport
{
    [JsonPropertyName("generated_at")]
    public required DateTime GeneratedAt { get; init; }

    [JsonPropertyName("documentation_coverage")]
    public required DocumentationCoverage DocumentationCoverage { get; init; }

    [JsonPropertyName("acceptance_criteria_coverage")]
    public required AcceptanceCriteriaCoverage AcceptanceCriteriaCoverage { get; init; }

    [JsonPropertyName("automation_coverage")]
    public required AutomationCoverage AutomationCoverage { get; init; }
}
