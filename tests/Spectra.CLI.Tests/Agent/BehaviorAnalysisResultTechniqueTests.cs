using Spectra.CLI.Agent.Analysis;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 037: BehaviorAnalysisResult exposes a TechniqueBreakdown map alongside
/// the existing category Breakdown. Defaults to empty for backward compatibility.
/// </summary>
public class BehaviorAnalysisResultTechniqueTests
{
    [Fact]
    public void TechniqueBreakdown_DefaultsToEmptyMap()
    {
        var result = new BehaviorAnalysisResult
        {
            TotalBehaviors = 0,
            Breakdown = new Dictionary<string, int>(),
            Behaviors = [],
            AlreadyCovered = 0,
            DocumentsAnalyzed = 0,
            TotalWords = 0
        };

        Assert.NotNull(result.TechniqueBreakdown);
        Assert.Empty(result.TechniqueBreakdown);
    }

    [Fact]
    public void TechniqueBreakdown_CanBeInitialized()
    {
        var result = new BehaviorAnalysisResult
        {
            TotalBehaviors = 2,
            Breakdown = new Dictionary<string, int> { ["boundary"] = 2 },
            TechniqueBreakdown = new Dictionary<string, int> { ["BVA"] = 2 },
            Behaviors = [],
            AlreadyCovered = 0,
            DocumentsAnalyzed = 1,
            TotalWords = 100
        };

        Assert.Single(result.TechniqueBreakdown);
        Assert.Equal(2, result.TechniqueBreakdown["BVA"]);
    }
}
