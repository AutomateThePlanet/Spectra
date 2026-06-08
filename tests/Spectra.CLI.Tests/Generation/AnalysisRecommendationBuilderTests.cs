using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Generation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 059 (US3) — token-free unit tests for the deterministic accounting relocated from
/// <c>BehaviorAnalyzer</c> onto the analyze seam. Covers the recommended-count math, category /
/// technique breakdowns, and fail-loud classification (empty → EmptyResponse, unparseable →
/// ParseFailure). No model, no I/O.
/// </summary>
public sealed class AnalysisRecommendationBuilderTests
{
    private const string Behaviors = """
        {"behaviors":[
          {"category":"happy_path","title":"Login succeeds with valid creds","source":"auth.md","technique":"UC"},
          {"category":"negative","title":"Login fails on bad password","source":"auth.md","technique":"EP"},
          {"category":"boundary","title":"Password at min length","source":"auth.md","technique":"bva"}
        ]}
        """;

    [Fact]
    public void Build_WellFormed_NoExistingTests_RecommendsAll()
    {
        var result = AnalysisRecommendationBuilder.Build(Behaviors, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AnalysisIngestOutcome.Recommendation, result.Outcome);
        Assert.Equal(3, result.TotalBehaviors);
        Assert.Equal(0, result.AlreadyCovered);
        Assert.Equal(3, result.RecommendedCount);
    }

    [Fact]
    public void Build_BuildsCategoryAndTechniqueBreakdowns()
    {
        var result = AnalysisRecommendationBuilder.Build(Behaviors, [], snapshot: null, focusArea: null);

        Assert.Equal(1, result.Breakdown["happy_path"]);
        Assert.Equal(1, result.Breakdown["negative"]);
        Assert.Equal(1, result.Breakdown["boundary"]);
        // Techniques are normalized to upper-invariant ("bva" → "BVA").
        Assert.Equal(1, result.TechniqueBreakdown["UC"]);
        Assert.Equal(1, result.TechniqueBreakdown["EP"]);
        Assert.Equal(1, result.TechniqueBreakdown["BVA"]);
    }

    [Fact]
    public void Build_RecommendedCount_FlooredAtZero_WhenSnapshotCoversAll()
    {
        // A coverage snapshot with an accurate existing-test count drives dedup (HasData → true).
        var snapshot = new CoverageSnapshot { ExistingTestCount = 5 };
        var result = AnalysisRecommendationBuilder.Build(Behaviors, [], snapshot, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.AlreadyCovered);
        Assert.Equal(0, result.RecommendedCount); // max(0, 3 - 5)
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_EmptyContent_IsEmptyResponse_FailLoud(string content)
    {
        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(AnalysisIngestOutcome.EmptyResponse, result.Outcome);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData("this is not json at all")]
    [InlineData("{\"behaviors\":[]}")]
    public void Build_NoBehaviors_IsParseFailure_FailLoud(string content)
    {
        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(AnalysisIngestOutcome.ParseFailure, result.Outcome);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Build_IsDeterministic_IdenticalInputsProduceIdenticalAccounting()
    {
        var a = AnalysisRecommendationBuilder.Build(Behaviors, [], null, null);
        var b = AnalysisRecommendationBuilder.Build(Behaviors, [], null, null);

        Assert.Equal(a.TotalBehaviors, b.TotalBehaviors);
        Assert.Equal(a.RecommendedCount, b.RecommendedCount);
        Assert.Equal(a.Breakdown, b.Breakdown);
        Assert.Equal(a.TechniqueBreakdown, b.TechniqueBreakdown);
    }
}
