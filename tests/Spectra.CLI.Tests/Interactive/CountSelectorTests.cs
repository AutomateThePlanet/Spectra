using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Interactive;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Interactive;

public class CountSelectorTests
{
    [Fact]
    public void BuildOptions_MultipleCategories_GeneratesCorrectOptions()
    {
        var analysis = new BehaviorAnalysisResult
        {
            TotalBehaviors = 18,
            Breakdown = new Dictionary<BehaviorCategory, int>
            {
                [BehaviorCategory.HappyPath] = 8,
                [BehaviorCategory.Negative] = 6,
                [BehaviorCategory.EdgeCase] = 3,
                [BehaviorCategory.Security] = 1
            },
            Behaviors = [],
            AlreadyCovered = 0,
            DocumentsAnalyzed = 3,
            TotalWords = 2400
        };

        var options = CountSelector.BuildOptions(analysis);

        // Should have: All, largest category, cumulative, custom, free-text
        Assert.True(options.Count >= 4);

        // First option: All
        Assert.Contains("All 18", options[0].Label);
        Assert.Equal(18, options[0].Count);

        // Second option: Happy paths only (largest category)
        Assert.Contains("happy paths only", options[1].Label);
        Assert.Equal(8, options[1].Count);
        Assert.NotNull(options[1].Categories);
        Assert.Contains(BehaviorCategory.HappyPath, options[1].Categories!);

        // Third option: Cumulative (happy + negative)
        Assert.Contains("happy paths + negative", options[2].Label);
        Assert.Equal(14, options[2].Count);

        // Custom number option exists
        Assert.Contains(options, o => o.IsCustomNumber);

        // Free text option exists
        Assert.Contains(options, o => o.IsFreeText);
    }

    [Fact]
    public void BuildOptions_SingleCategory_NoSingleCategoryOption()
    {
        var analysis = new BehaviorAnalysisResult
        {
            TotalBehaviors = 5,
            Breakdown = new Dictionary<BehaviorCategory, int>
            {
                [BehaviorCategory.HappyPath] = 5
            },
            Behaviors = [],
            AlreadyCovered = 0,
            DocumentsAnalyzed = 1,
            TotalWords = 500
        };

        var options = CountSelector.BuildOptions(analysis);

        // Should have: All, custom, free-text (no single category or cumulative)
        Assert.Equal(3, options.Count);
        Assert.Contains("All 5", options[0].Label);
        Assert.True(options[1].IsCustomNumber);
        Assert.True(options[2].IsFreeText);
    }

    [Fact]
    public void BuildOptions_WithCoveredTests_UsesRecommendedCount()
    {
        var analysis = new BehaviorAnalysisResult
        {
            TotalBehaviors = 18,
            Breakdown = new Dictionary<BehaviorCategory, int>
            {
                [BehaviorCategory.HappyPath] = 8,
                [BehaviorCategory.Negative] = 6,
                [BehaviorCategory.EdgeCase] = 3,
                [BehaviorCategory.Security] = 1
            },
            Behaviors = [],
            AlreadyCovered = 10,
            DocumentsAnalyzed = 3,
            TotalWords = 2400
        };

        var options = CountSelector.BuildOptions(analysis);

        // First option should use RecommendedCount (8) not TotalBehaviors (18)
        Assert.Contains("All 8", options[0].Label);
        Assert.Equal(8, options[0].Count);
    }

    [Fact]
    public void BuildOptions_TwoCategories_NoCumulativeOption()
    {
        var analysis = new BehaviorAnalysisResult
        {
            TotalBehaviors = 10,
            Breakdown = new Dictionary<BehaviorCategory, int>
            {
                [BehaviorCategory.HappyPath] = 7,
                [BehaviorCategory.Negative] = 3
            },
            Behaviors = [],
            AlreadyCovered = 0,
            DocumentsAnalyzed = 2,
            TotalWords = 1000
        };

        var options = CountSelector.BuildOptions(analysis);

        // With only 2 categories, cumulative would equal "All" — so no cumulative option
        // Should have: All, single category, custom, free-text
        Assert.Equal(4, options.Count);
        Assert.Contains("All 10", options[0].Label);
        Assert.Contains("happy paths only", options[1].Label);
    }

    [Fact]
    public void BuildOptions_ZeroCountCategories_Excluded()
    {
        var analysis = new BehaviorAnalysisResult
        {
            TotalBehaviors = 8,
            Breakdown = new Dictionary<BehaviorCategory, int>
            {
                [BehaviorCategory.HappyPath] = 8,
                [BehaviorCategory.Negative] = 0,
                [BehaviorCategory.EdgeCase] = 0
            },
            Behaviors = [],
            AlreadyCovered = 0,
            DocumentsAnalyzed = 1,
            TotalWords = 500
        };

        var options = CountSelector.BuildOptions(analysis);

        // Zero-count categories should not generate separate options
        Assert.DoesNotContain(options, o => o.Label.Contains("negative"));
        Assert.DoesNotContain(options, o => o.Label.Contains("edge"));
    }
}
