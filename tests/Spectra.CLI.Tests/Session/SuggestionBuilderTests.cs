using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Session;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Session;

public class SuggestionBuilderTests
{
    [Fact]
    public void Build_WithRemainingBehaviors_ReturnsSuggestions()
    {
        var analysis = CreateAnalysis(totalBehaviors: 10, alreadyCovered: 2, breakdown: new()
        {
            [BehaviorCategory.HappyPath] = 4,
            [BehaviorCategory.Negative] = 3,
            [BehaviorCategory.EdgeCase] = 2,
            [BehaviorCategory.Security] = 1
        });

        var suggestions = SuggestionBuilder.Build(analysis, generatedCount: 3);
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.True(s.Index > 0));
        Assert.All(suggestions, s => Assert.NotEmpty(s.Title));
        Assert.All(suggestions, s => Assert.Equal(SuggestionStatus.Pending, s.Status));
    }

    [Fact]
    public void Build_AllCovered_ReturnsEmpty()
    {
        var analysis = CreateAnalysis(totalBehaviors: 5, alreadyCovered: 3, breakdown: new()
        {
            [BehaviorCategory.HappyPath] = 3,
            [BehaviorCategory.Negative] = 2
        });

        var suggestions = SuggestionBuilder.Build(analysis, generatedCount: 2);
        Assert.Empty(suggestions);
    }

    [Fact]
    public void Build_IndicesAreSequential()
    {
        var analysis = CreateAnalysis(totalBehaviors: 20, alreadyCovered: 2, breakdown: new()
        {
            [BehaviorCategory.HappyPath] = 8,
            [BehaviorCategory.Negative] = 6,
            [BehaviorCategory.EdgeCase] = 4,
            [BehaviorCategory.Security] = 2
        });

        var suggestions = SuggestionBuilder.Build(analysis, generatedCount: 5);
        for (var i = 0; i < suggestions.Count; i++)
        {
            Assert.Equal(i + 1, suggestions[i].Index);
        }
    }

    [Fact]
    public void Build_ZeroBehaviors_ReturnsEmpty()
    {
        var analysis = CreateAnalysis(totalBehaviors: 0, alreadyCovered: 0, breakdown: new());

        var suggestions = SuggestionBuilder.Build(analysis, generatedCount: 0);
        Assert.Empty(suggestions);
    }

    private static BehaviorAnalysisResult CreateAnalysis(
        int totalBehaviors,
        int alreadyCovered,
        Dictionary<BehaviorCategory, int> breakdown)
    {
        return new BehaviorAnalysisResult
        {
            TotalBehaviors = totalBehaviors,
            AlreadyCovered = alreadyCovered,
            DocumentsAnalyzed = 3,
            TotalWords = 1000,
            Breakdown = breakdown,
            Behaviors = []
        };
    }
}
