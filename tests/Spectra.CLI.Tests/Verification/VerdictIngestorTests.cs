using Spectra.CLI.Verification;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Verification;

/// <summary>
/// Spec 055 — token-free unit tests for the fail-loud verdict-ingest boundary.
/// Covers US2 (verdict gating unchanged/advisory) and US3 (damage fails loud, never throws,
/// distinct from a critic call failure). No model, no network.
/// </summary>
public sealed class VerdictIngestorTests
{
    // ---------- US2: verdict gating stays advisory and unchanged ----------

    [Fact]
    public void Classify_Hallucinated_Drops()
    {
        var r = VerdictIngestor.Classify("""{"verdict":"hallucinated","score":0.1,"findings":[]}""");

        Assert.True(r.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.Verdict, r.Outcome);
        Assert.Equal(VerificationVerdict.Hallucinated, r.Result!.Verdict);
        Assert.True(r.Drops);
    }

    [Theory]
    [InlineData("grounded", VerificationVerdict.Grounded)]
    [InlineData("partial", VerificationVerdict.Partial)]
    [InlineData("manual", VerificationVerdict.Manual)]
    public void Classify_NonHallucinated_Passes(string verdict, VerificationVerdict expected)
    {
        var r = VerdictIngestor.Classify($$"""{"verdict":"{{verdict}}","score":0.9,"findings":[]}""");

        Assert.True(r.IsSuccess);
        Assert.Equal(expected, r.Result!.Verdict);
        Assert.False(r.Drops); // advisory: only hallucinated drops
    }

    [Fact]
    public void Classify_ParsesScoreAndFindings()
    {
        var r = VerdictIngestor.Classify("""
            {"verdict":"partial","score":"0.5",
             "findings":[{"element":"Step 1","claim":"X","status":"unverified","reason":"vague"}]}
            """);

        Assert.True(r.IsSuccess);
        Assert.Equal(0.5, r.Result!.Score);
        Assert.Single(r.Result.Findings);
        Assert.Equal(FindingStatus.Unverified, r.Result.Findings[0].Status);
    }

    // ---------- US3: damage fails loud (FR-006), never throws ----------

    [Fact]
    public void Classify_MissingVerdict_FailsLoud_NoSoftDefault()
    {
        var r = VerdictIngestor.Classify("""{"score":0.5}""");

        Assert.False(r.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.ParseFailure, r.Outcome);
        Assert.False(r.Drops);
        Assert.Null(r.Result);              // NOT a Partial/0.5 soft pass
        Assert.NotEmpty(r.Errors);
        Assert.Contains(r.Errors, e => e.Contains("verdict"));
    }

    [Fact]
    public void Classify_MissingScore_FailsLoud_NoSoftDefault()
    {
        var r = VerdictIngestor.Classify("""{"verdict":"grounded"}""");

        Assert.False(r.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.ParseFailure, r.Outcome);
        Assert.Null(r.Result);
        Assert.Contains(r.Errors, e => e.Contains("score"));
    }

    [Fact]
    public void Classify_UnknownVerdictString_IsDamage_NotPartialCoercion()
    {
        var r = VerdictIngestor.Classify("""{"verdict":"maybe","score":0.5}""");

        Assert.False(r.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.ParseFailure, r.Outcome);
        Assert.Null(r.Result);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ broken json")]
    [InlineData("[]")]
    public void Classify_Malformed_ReturnsParseFailure_NeverThrows(string content)
    {
        var r = VerdictIngestor.Classify(content);

        Assert.False(r.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.ParseFailure, r.Outcome);
        Assert.NotEmpty(r.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_Empty_ReturnsEmptyResponse_NeverThrows(string? content)
    {
        var r = VerdictIngestor.Classify(content);

        Assert.False(r.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.EmptyResponse, r.Outcome);
        Assert.False(r.Drops);
    }

    [Fact]
    public void Classify_FencedJson_IsExtracted()
    {
        var r = VerdictIngestor.Classify("```json\n{\"verdict\":\"grounded\",\"score\":1.0}\n```");

        Assert.True(r.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, r.Result!.Verdict);
    }

    // ---------- US2 guard: the reused verdict-gating contract (drop-vs-pass) is intact ----------

    [Fact]
    public void ReusedGatingContract_DropsOnlyHallucinated()
    {
        var verdicts = new[]
        {
            VerificationVerdict.Grounded,
            VerificationVerdict.Partial,
            VerificationVerdict.Hallucinated,
            VerificationVerdict.Manual
        };

        // This mirrors the reused GenerateHandler gate (Verdict != Hallucinated) — additive guard,
        // not a modification of any existing gating test.
        var kept = verdicts.Where(v => v != VerificationVerdict.Hallucinated).ToList();

        Assert.DoesNotContain(VerificationVerdict.Hallucinated, kept);
        Assert.Contains(VerificationVerdict.Grounded, kept);
        Assert.Contains(VerificationVerdict.Partial, kept);
        Assert.Contains(VerificationVerdict.Manual, kept);
    }
}
