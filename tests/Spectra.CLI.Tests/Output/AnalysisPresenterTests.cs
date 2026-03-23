using Spectra.CLI.Agent.Analysis;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Output;

public class AnalysisPresenterTests
{
    [Fact]
    public void BehaviorAnalysisResult_RecommendedCount_SubtractsCovered()
    {
        var result = new BehaviorAnalysisResult
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

        Assert.Equal(8, result.RecommendedCount);
    }

    [Fact]
    public void BehaviorAnalysisResult_RecommendedCount_NeverNegative()
    {
        var result = new BehaviorAnalysisResult
        {
            TotalBehaviors = 5,
            Breakdown = new Dictionary<BehaviorCategory, int>
            {
                [BehaviorCategory.HappyPath] = 5
            },
            Behaviors = [],
            AlreadyCovered = 10, // More covered than total
            DocumentsAnalyzed = 1,
            TotalWords = 100
        };

        Assert.Equal(0, result.RecommendedCount);
    }

    [Fact]
    public void GetRemainingByCategory_NoGeneratedCategories_ReturnsFullBreakdown()
    {
        var result = new BehaviorAnalysisResult
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

        var remaining = result.GetRemainingByCategory();

        Assert.Equal(4, remaining.Count);
        Assert.Equal(8, remaining[BehaviorCategory.HappyPath]);
    }

    [Fact]
    public void GetRemainingByCategory_WithGeneratedCategories_ZerosOutGenerated()
    {
        var result = new BehaviorAnalysisResult
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

        var remaining = result.GetRemainingByCategory([BehaviorCategory.HappyPath]);

        Assert.Equal(3, remaining.Count); // HappyPath zeroed out and removed
        Assert.False(remaining.ContainsKey(BehaviorCategory.HappyPath));
        Assert.Equal(6, remaining[BehaviorCategory.Negative]);
    }
}
