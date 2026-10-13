using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Generation;
using Spectra.Core.Models;

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

    // ---- Spec 095 (FR-001–FR-003): one per-behaviour coverage rule; replaces the 044 row
    // Build_RecommendedCount_FlooredAtZero_WhenSnapshotCoversAll (already_covered was the suite's test count). ----

    private static TestCase Test(string id, string title) => new()
    {
        Id = id, Title = title, Priority = Priority.Medium, Steps = ["step"], ExpectedResult = "ok", FilePath = ""
    };

    private static string Analysis(params string[] titles) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            behaviors = titles.Select((t, i) => new { category = i % 2 == 0 ? "happy_path" : "negative", title = t, source = "docs/a.md", technique = "EP" })
        });

    [Fact]
    public void Build_ASnapshot_NoLongerDecidesTheCoveredCount()
    {
        var snapshot = new CoverageSnapshot { ExistingTestCount = 5 };

        var withSnapshot = AnalysisRecommendationBuilder.Build(Behaviors, [], snapshot, focusArea: null);
        var withoutSnapshot = AnalysisRecommendationBuilder.Build(Behaviors, [], snapshot: null, focusArea: null);

        Assert.Equal(0, withSnapshot.AlreadyCovered);
        Assert.Equal(3, withSnapshot.RecommendedCount);
        Assert.Equal(withoutSnapshot.AlreadyCovered, withSnapshot.AlreadyCovered);
        Assert.Equal(withoutSnapshot.RecommendedCount, withSnapshot.RecommendedCount);
    }

    [Fact]
    public void Build_FiftyTests_TwelveGaps_RecommendsTwelve_WithAndWithoutASnapshot()
    {
        var tests = Enumerable.Range(1, 50).Select(i => Test($"TC-{i:D3}", $"Existing behaviour number {i} of the archive module")).ToList();
        var titles = new List<string>
        {
            "Existing behaviour number 7 of the archive module",
            "Existing behaviour number 21 of the archive module",
            "Existing behaviour number 40 of the archive module",
        };
        titles.AddRange(Enumerable.Range(1, 12).Select(i => $"Quota warning banner variant {(char)('A' + i)} turns red"));
        var content = Analysis([.. titles]);

        var withSnapshot = AnalysisRecommendationBuilder.Build(content, tests, new CoverageSnapshot { ExistingTestCount = 50 }, focusArea: null);
        var withoutSnapshot = AnalysisRecommendationBuilder.Build(content, tests, snapshot: null, focusArea: null);

        foreach (var result in new[] { withSnapshot, withoutSnapshot })
        {
            Assert.Equal(3, result.AlreadyCovered);
            Assert.Equal(12, result.RecommendedCount);
            Assert.Equal(15, result.TotalBehaviors);
        }
    }

    [Fact]
    public void Build_TheThreshold_ComesFromTheCaller()
    {
        var tests = new[] { Test("TC-101", "Upload above quota is refused now") };
        var content = Analysis("Upload above the quota is refused");

        Assert.Equal(1, AnalysisRecommendationBuilder.Build(content, tests, null, null).AlreadyCovered);
        Assert.Equal(0, AnalysisRecommendationBuilder.Build(content, tests, null, null, duplicateThreshold: 0.9).AlreadyCovered);
    }

    [Fact]
    public void Build_CoveredBy_IsPerBehaviour_TiesToTheLowestId()
    {
        var tests = new[] { Test("TC-120", "Quota shown in settings"), Test("TC-105", "Quota shown in settings") };

        var result = AnalysisRecommendationBuilder.Build(Analysis("Quota shown in settings", "Folder nested twenty levels deep"), tests, null, null);

        Assert.Equal(new string?[] { "TC-105", null }, result.CoveredBy);
        Assert.Equal(1, result.AlreadyCovered);
        Assert.Equal(1, result.RecommendedCount);
    }

    [Fact]
    public void Build_TheBreakdowns_StillCoverAllBehaviours()
    {
        var tests = new[] { Test("TC-101", "Quota shown in settings") };

        var result = AnalysisRecommendationBuilder.Build(Analysis("Quota shown in settings", "Folder nested twenty levels deep"), tests, null, null);

        Assert.Equal(1, result.Breakdown["happy_path"]);
        Assert.Equal(1, result.Breakdown["negative"]);
        Assert.Equal(2, result.TechniqueBreakdown["EP"]);
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
    public void Build_WellFormed_NoBoundaryGaps_YieldsEmptyBoundaryGaps()
    {
        // Spec 062: the legacy behaviors-only payload still produces a clean (empty) boundary set
        // alongside the existing technique_breakdown — additive, backward-compatible.
        var result = AnalysisRecommendationBuilder.Build(Behaviors, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.BoundaryGaps);
        Assert.Equal(1, result.TechniqueBreakdown["BVA"]);
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
