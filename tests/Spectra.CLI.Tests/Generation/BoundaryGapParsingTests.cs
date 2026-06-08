using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Generation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 062 — token-free unit tests for boundary-coverage gap detection on the analysis seam.
/// Covers: detection of implied-but-uncovered boundaries (US1), the no-condition / legacy cases
/// (US1/FR-004), strict fail-loud on a malformed boundary_gaps payload (US2/FR-003), and the
/// advisory-invariance guarantee that gaps never alter the accounting (US3/FR-005). No model, no I/O.
/// </summary>
public sealed class BoundaryGapParsingTests
{
    private const string WithGaps = """
        {"behaviors":[
          {"category":"boundary","title":"Username at max length","source":"signup.md","technique":"BVA"}
        ],
        "boundary_gaps":[
          {"field":"username","kind":"max-length","description":"21-char input (max 20) untested","source":"docs/signup.md"},
          {"field":"order total","kind":"min-max","description":"negative total not tested","source":""}
        ]}
        """;

    private const string NoGapsKey = """
        {"behaviors":[
          {"category":"happy_path","title":"Login succeeds","source":"auth.md","technique":"UC"}
        ]}
        """;

    // ---- US1: detection + carry (FR-002) ----

    [Fact]
    public void Build_WellFormedBoundaryGaps_AreCarried()
    {
        var result = AnalysisRecommendationBuilder.Build(WithGaps, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.BoundaryGaps.Count);

        var first = result.BoundaryGaps[0];
        Assert.Equal("username", first.Field);
        Assert.Equal("max-length", first.Kind);
        Assert.Equal("21-char input (max 20) untested", first.Description);
        Assert.Equal("docs/signup.md", first.Source);

        // source may be empty (inferred) without being malformed.
        Assert.Equal("", result.BoundaryGaps[1].Source);
    }

    [Fact]
    public void Build_BoundaryGaps_CoexistWithTechniqueBreakdown()
    {
        var result = AnalysisRecommendationBuilder.Build(WithGaps, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.TechniqueBreakdown["BVA"]);   // breakdown still produced
        Assert.NotEmpty(result.BoundaryGaps);                // alongside the gaps (FR-003 "carried alongside")
    }

    // ---- US1/FR-004: no spurious gaps ----

    [Fact]
    public void Build_NoBoundaryGapsKey_YieldsEmpty_AndSucceeds()
    {
        var result = AnalysisRecommendationBuilder.Build(NoGapsKey, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.BoundaryGaps);
    }

    [Fact]
    public void Build_EmptyBoundaryGapsArray_YieldsEmpty_AndSucceeds()
    {
        var content = """
            {"behaviors":[{"category":"x","title":"t","source":"s.md","technique":"EP"}],"boundary_gaps":[]}
            """;

        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.BoundaryGaps);
    }

    [Fact]
    public void Build_BareBehaviorsArray_NoBoundaryGaps_Succeeds()
    {
        // Legacy bare-array form (no top-level object) → no boundary_gaps, not malformed.
        var content = """
            [{"category":"x","title":"t","source":"s.md","technique":"EP"}]
            """;

        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.BoundaryGaps);
    }

    // ---- US2/FR-003: strict fail-loud on malformed payload ----

    [Fact]
    public void Build_BoundaryGapsNotArray_IsParseFailure_WithSpecificError()
    {
        var content = """
            {"behaviors":[{"category":"x","title":"t","source":"s.md","technique":"EP"}],"boundary_gaps":{"field":"x"}}
            """;

        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(AnalysisIngestOutcome.ParseFailure, result.Outcome);
        Assert.Contains(result.Errors, e => e.Contains("boundary_gaps must be a JSON array"));
    }

    [Theory]
    [InlineData("field")]
    [InlineData("kind")]
    [InlineData("description")]
    public void Build_BoundaryGapMissingRequiredField_IsParseFailure_NamingIndexAndField(string missing)
    {
        // Build an element that omits exactly the named required field.
        var field = missing == "field" ? "" : "\"field\":\"username\",";
        var kind = missing == "kind" ? "" : "\"kind\":\"max-length\",";
        var description = missing == "description" ? "" : "\"description\":\"untested\",";
        var element = "{" + field + kind + description + "\"source\":\"s.md\"}";
        var content =
            "{\"behaviors\":[{\"category\":\"x\",\"title\":\"t\",\"source\":\"s.md\",\"technique\":\"EP\"}]," +
            "\"boundary_gaps\":[" + element + "]}";

        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(AnalysisIngestOutcome.ParseFailure, result.Outcome);
        Assert.Contains(result.Errors, e => e.Contains("boundary_gaps[0]") && e.Contains($"'{missing}'"));
    }

    [Fact]
    public void Build_BoundaryGapElementNotObject_IsParseFailure()
    {
        var content = """
            {"behaviors":[{"category":"x","title":"t","source":"s.md","technique":"EP"}],"boundary_gaps":["not an object"]}
            """;

        var result = AnalysisRecommendationBuilder.Build(content, [], snapshot: null, focusArea: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(AnalysisIngestOutcome.ParseFailure, result.Outcome);
        Assert.Contains(result.Errors, e => e.Contains("boundary_gaps[0]"));
    }

    // ---- US3/FR-005: advisory invariance — gaps never change the accounting ----

    [Fact]
    public void Build_BoundaryGaps_DoNotAlterAccounting()
    {
        var withGaps = AnalysisRecommendationBuilder.Build(WithGaps, [], snapshot: null, focusArea: null);

        // Same single behavior, no boundary_gaps key.
        var withoutGaps = AnalysisRecommendationBuilder.Build(
            """
            {"behaviors":[{"category":"boundary","title":"Username at max length","source":"signup.md","technique":"BVA"}]}
            """, [], snapshot: null, focusArea: null);

        Assert.Equal(withoutGaps.TotalBehaviors, withGaps.TotalBehaviors);
        Assert.Equal(withoutGaps.RecommendedCount, withGaps.RecommendedCount);
        Assert.Equal(withoutGaps.AlreadyCovered, withGaps.AlreadyCovered);
        Assert.Equal(withoutGaps.DocumentsAnalyzed, withGaps.DocumentsAnalyzed);
        Assert.Equal(withoutGaps.Breakdown, withGaps.Breakdown);
        Assert.Equal(withoutGaps.TechniqueBreakdown, withGaps.TechniqueBreakdown);
        Assert.Equal(withoutGaps.Outcome, withGaps.Outcome);
    }
}
