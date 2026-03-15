using Spectra.Core.Models;
using CoverageModels = Spectra.Core.Models.Coverage;

namespace Spectra.Core.Coverage;

/// <summary>
/// Calculates coverage statistics from reconciliation results.
/// </summary>
public sealed class CoverageCalculator
{
    /// <summary>
    /// Calculates complete coverage report from suite indexes and reconciliation result.
    /// </summary>
    public CoverageModels.CoverageReport Calculate(
        IReadOnlyDictionary<string, MetadataIndex> suiteIndexes,
        ReconciliationResult reconciliation)
    {
        var allTests = suiteIndexes.Values
            .SelectMany(idx => idx.Tests)
            .ToList();

        var automatedTestIds = reconciliation.ValidLinks
            .Where(l => l.Status == CoverageModels.LinkStatus.Valid)
            .Select(l => l.Source)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summary = CalculateSummary(allTests, automatedTestIds, reconciliation);
        var bySuite = CalculateBySuite(suiteIndexes, automatedTestIds);
        var byComponent = CalculateByComponent(allTests, automatedTestIds);

        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = summary,
            BySuite = bySuite,
            ByComponent = byComponent,
            UnlinkedTests = reconciliation.UnlinkedTests,
            OrphanedAutomation = reconciliation.OrphanedAutomation,
            BrokenLinks = reconciliation.BrokenLinks,
            Mismatches = reconciliation.Mismatches
        };
    }

    /// <summary>
    /// Calculates coverage percentage for a single suite.
    /// </summary>
    public decimal CalculateSuiteCoverage(
        MetadataIndex suiteIndex,
        ReconciliationResult reconciliation)
    {
        var automatedTestIds = reconciliation.ValidLinks
            .Where(l => l.Status == CoverageModels.LinkStatus.Valid)
            .Select(l => l.Source)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var total = suiteIndex.Tests.Count;
        if (total == 0) return 0m;

        var automated = suiteIndex.Tests.Count(t => automatedTestIds.Contains(t.Id));
        return (automated * 100m) / total;
    }

    /// <summary>
    /// Calculates coverage percentage for a component.
    /// </summary>
    public decimal CalculateComponentCoverage(
        IEnumerable<TestIndexEntry> tests,
        ReconciliationResult reconciliation)
    {
        var automatedTestIds = reconciliation.ValidLinks
            .Where(l => l.Status == CoverageModels.LinkStatus.Valid)
            .Select(l => l.Source)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var testList = tests.ToList();
        var total = testList.Count;
        if (total == 0) return 0m;

        var automated = testList.Count(t => automatedTestIds.Contains(t.Id));
        return (automated * 100m) / total;
    }

    /// <summary>
    /// Calculates aggregate summary statistics.
    /// </summary>
    private static CoverageModels.CoverageSummary CalculateSummary(
        IReadOnlyList<TestIndexEntry> allTests,
        HashSet<string> automatedTestIds,
        ReconciliationResult reconciliation)
    {
        var total = allTests.Count;
        var automated = allTests.Count(t => automatedTestIds.Contains(t.Id));
        var manualOnly = total - automated;
        var coveragePercentage = total > 0 ? (automated * 100m) / total : 0m;

        return new CoverageModels.CoverageSummary
        {
            TotalTests = total,
            Automated = automated,
            ManualOnly = manualOnly,
            CoveragePercentage = Math.Round(coveragePercentage, 2),
            BrokenLinks = reconciliation.BrokenLinks.Count,
            OrphanedAutomation = reconciliation.OrphanedAutomation.Count,
            Mismatches = reconciliation.Mismatches.Count
        };
    }

    /// <summary>
    /// Calculates coverage statistics per suite.
    /// </summary>
    private static IReadOnlyList<CoverageModels.SuiteCoverage> CalculateBySuite(
        IReadOnlyDictionary<string, MetadataIndex> suiteIndexes,
        HashSet<string> automatedTestIds)
    {
        var results = new List<CoverageModels.SuiteCoverage>();

        foreach (var (suite, index) in suiteIndexes)
        {
            var total = index.Tests.Count;
            var automated = index.Tests.Count(t => automatedTestIds.Contains(t.Id));
            var coveragePercentage = total > 0 ? (automated * 100m) / total : 0m;

            results.Add(new CoverageModels.SuiteCoverage
            {
                Suite = suite,
                Total = total,
                Automated = automated,
                CoveragePercentage = Math.Round(coveragePercentage, 2)
            });
        }

        return results.OrderBy(s => s.Suite).ToList();
    }

    /// <summary>
    /// Calculates coverage statistics per component.
    /// </summary>
    private static IReadOnlyList<CoverageModels.ComponentCoverage> CalculateByComponent(
        IReadOnlyList<TestIndexEntry> allTests,
        HashSet<string> automatedTestIds)
    {
        var byComponent = allTests
            .Where(t => !string.IsNullOrEmpty(t.Component))
            .GroupBy(t => t.Component!);

        var results = new List<CoverageModels.ComponentCoverage>();

        foreach (var group in byComponent)
        {
            var tests = group.ToList();
            var total = tests.Count;
            var automated = tests.Count(t => automatedTestIds.Contains(t.Id));
            var coveragePercentage = total > 0 ? (automated * 100m) / total : 0m;

            results.Add(new CoverageModels.ComponentCoverage
            {
                Component = group.Key,
                Total = total,
                Automated = automated,
                CoveragePercentage = Math.Round(coveragePercentage, 2)
            });
        }

        return results.OrderBy(c => c.Component).ToList();
    }
}
