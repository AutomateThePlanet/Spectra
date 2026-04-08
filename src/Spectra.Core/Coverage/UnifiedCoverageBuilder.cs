using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Coverage;

/// <summary>
/// Composes the three coverage sections into a unified report.
/// </summary>
public sealed class UnifiedCoverageBuilder
{
    /// <summary>
    /// Builds a unified coverage report from the three coverage analyses.
    /// </summary>
    public UnifiedCoverageReport Build(
        DocumentationCoverage docCoverage,
        AcceptanceCriteriaCoverage reqCoverage,
        AutomationCoverage autoCoverage)
    {
        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = docCoverage,
            AcceptanceCriteriaCoverage = reqCoverage,
            AutomationCoverage = autoCoverage
        };
    }

    /// <summary>
    /// Projects a CoverageCalculator result into the AutomationCoverage model.
    /// </summary>
    public static AutomationCoverage FromCalculatorReport(CoverageReport report)
    {
        return new AutomationCoverage
        {
            TotalTests = report.Summary.TotalTests,
            Automated = report.Summary.Automated,
            Percentage = report.Summary.CoveragePercentage,
            BySuite = report.BySuite,
            UnlinkedTests = report.UnlinkedTests,
            OrphanedAutomation = report.OrphanedAutomation,
            BrokenLinks = report.BrokenLinks
        };
    }
}
