using Spectra.CLI.Coverage;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Coverage;

/// <summary>
/// Spec 060 — excluded docs must be visibly reported with a distinct "excluded"
/// status (FR-003), never silently dropped, in both the markdown and compact
/// (text) renderers of the unified report.
/// </summary>
public class CoverageReportWriterExclusionTests
{
    private readonly CoverageReportWriter _writer = new();

    [Fact]
    public void FormatAsMarkdown_ExcludedDoc_RendersExcludedStatusAndSummary()
    {
        var report = BuildReport(excluded: true);

        var md = _writer.FormatAsMarkdown(report);

        // Distinct status in the table (not "Yes"/"No").
        Assert.Contains("| docs/release-notes/v1.md | 0 | Excluded |", md);
        // Summary surfaces the excluded count.
        Assert.Contains("(1 excluded)", md);
    }

    [Fact]
    public void FormatAsText_ExcludedDoc_UsesDistinctMark()
    {
        var report = BuildReport(excluded: true);

        var text = _writer.FormatAsText(report);

        // Distinct '~' mark, separate from covered '+' and uncovered '-'.
        Assert.Contains("[~] docs/release-notes/v1.md", text);
        Assert.Contains("(1 excluded)", text);
    }

    [Fact]
    public void FormatAsMarkdown_NoExclusions_NoExcludedStatusOrSummary()
    {
        var report = BuildReport(excluded: false);

        var md = _writer.FormatAsMarkdown(report);

        Assert.DoesNotContain("Excluded", md);
        Assert.DoesNotContain("excluded)", md);
    }

    private static UnifiedCoverageReport BuildReport(bool excluded)
    {
        var details = new List<DocumentCoverageDetail>
        {
            new()
            {
                Doc = "docs/login.md",
                TestCount = 2,
                Covered = true,
                TestIds = ["TC-001", "TC-002"]
            }
        };

        if (excluded)
        {
            details.Add(new DocumentCoverageDetail
            {
                Doc = "docs/release-notes/v1.md",
                TestCount = 0,
                Covered = false,
                Excluded = true,
                ExcludedByPattern = "docs/release-notes/**"
            });
        }

        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = new DocumentationCoverage
            {
                TotalDocs = 1,
                CoveredDocs = 1,
                Percentage = 100m,
                ExcludedDocs = excluded ? 1 : 0,
                Details = details
            },
            AcceptanceCriteriaCoverage = new AcceptanceCriteriaCoverage
            {
                TotalCriteria = 0,
                CoveredCriteria = 0,
                Percentage = 0m,
                HasCriteriaFile = false
            },
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 2,
                Automated = 1,
                Percentage = 50m
            }
        };
    }
}
