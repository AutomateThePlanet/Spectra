using System.Text.Json.Serialization;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Three-section coverage summary for dashboard display.
/// </summary>
public sealed class CoverageSummaryData
{
    [JsonPropertyName("documentation")]
    public required DocumentationSectionData Documentation { get; init; }

    [JsonPropertyName("requirements")]
    public required RequirementsSectionData Requirements { get; init; }

    [JsonPropertyName("automation")]
    public required AutomationSectionData Automation { get; init; }
}

/// <summary>
/// Documentation coverage section with per-document detail.
/// </summary>
public sealed class DocumentationSectionData
{
    [JsonPropertyName("covered")]
    public required int Covered { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DocumentationCoverageDetail>? Details { get; init; }

    [JsonPropertyName("undocumented_test_count")]
    public int UndocumentedTestCount { get; init; }

    [JsonPropertyName("undocumented_test_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? UndocumentedTestIds { get; init; }
}

/// <summary>
/// Requirements coverage section with per-requirement detail.
/// </summary>
public sealed class RequirementsSectionData
{
    [JsonPropertyName("covered")]
    public required int Covered { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("has_requirements_file")]
    public required bool HasRequirementsFile { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RequirementCoverageDetail>? Details { get; init; }
}

/// <summary>
/// Automation coverage section with per-suite detail.
/// </summary>
public sealed class AutomationSectionData
{
    [JsonPropertyName("covered")]
    public required int Covered { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AutomationSuiteDetail>? Details { get; init; }

    [JsonPropertyName("unlinked_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UnlinkedTestDetail>? UnlinkedTests { get; init; }
}

/// <summary>
/// Per-document coverage detail.
/// </summary>
public sealed class DocumentationCoverageDetail
{
    [JsonPropertyName("doc")]
    public required string Doc { get; init; }

    [JsonPropertyName("test_count")]
    public required int TestCount { get; init; }

    [JsonPropertyName("covered")]
    public required bool Covered { get; init; }

    [JsonPropertyName("test_ids")]
    public IReadOnlyList<string> TestIds { get; init; } = [];
}

/// <summary>
/// Per-suite automation coverage detail.
/// </summary>
public sealed class AutomationSuiteDetail
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("automated")]
    public required int Automated { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }
}

/// <summary>
/// Detail for a test without automation link.
/// </summary>
public sealed class UnlinkedTestDetail
{
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }
}
