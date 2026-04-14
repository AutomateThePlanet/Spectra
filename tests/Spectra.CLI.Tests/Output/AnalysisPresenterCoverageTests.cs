using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;

namespace Spectra.CLI.Tests.Output;

public sealed class AnalysisPresenterCoverageTests
{
    [Fact]
    public void DisplayBreakdown_WithSnapshot_DoesNotThrow()
    {
        var result = new BehaviorAnalysisResult
        {
            TotalBehaviors = 8,
            Breakdown = new Dictionary<string, int> { ["happy_path"] = 3, ["boundary"] = 5 },
            Behaviors = [],
            AlreadyCovered = 231,
            DocumentsAnalyzed = 5,
            TotalWords = 10000
        };

        var snapshot = new CoverageSnapshot
        {
            ExistingTestCount = 231,
            ExistingTestTitles = ["Test 1", "Test 2"],
            CoveredCriteriaIds = new HashSet<string> { "AC-001", "AC-002" },
            UncoveredCriteria = [new UncoveredCriterion("AC-003", "Third", null, "high")],
            CoveredSourceRefs = new HashSet<string> { "docs/api.md" },
            UncoveredSourceRefs = ["docs/auth.md"],
            TotalCriteriaCount = 3
        };

        // Should not throw even when Console.IsOutputRedirected is true
        AnalysisPresenter.DisplayBreakdown(result, OutputFormat.Json, snapshot);
    }

    [Fact]
    public void DisplayAllCovered_WithSnapshot_ZeroGap_DoesNotThrow()
    {
        var snapshot = new CoverageSnapshot
        {
            ExistingTestCount = 100,
            TotalCriteriaCount = 10,
            CoveredCriteriaIds = new HashSet<string>(Enumerable.Range(1, 10).Select(i => $"AC-{i:D3}")),
            UncoveredCriteria = []
        };

        // Should not throw
        AnalysisPresenter.DisplayAllCovered(50, OutputFormat.Json, snapshot);
    }

    [Fact]
    public void DisplayBreakdown_WithoutSnapshot_DoesNotThrow()
    {
        var result = new BehaviorAnalysisResult
        {
            TotalBehaviors = 142,
            Breakdown = new Dictionary<string, int> { ["happy_path"] = 50, ["negative"] = 92 },
            Behaviors = [],
            AlreadyCovered = 3,
            DocumentsAnalyzed = 10,
            TotalWords = 5000
        };

        // Without snapshot — backwards-compatible
        AnalysisPresenter.DisplayBreakdown(result, OutputFormat.Json);
    }
}
