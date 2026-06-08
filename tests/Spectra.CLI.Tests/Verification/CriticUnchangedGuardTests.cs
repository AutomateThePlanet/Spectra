using Spectra.CLI.Verification;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Verification;

/// <summary>
/// Spec 062 (US3 / FR-001 / SC-005) — guard that boundary-coverage gap detection landed on the
/// ANALYSIS seam (seam b) and left the grounding critic untouched. These assertions fail iff
/// someone wrongly added a completeness/boundary dimension to the critic (seam a): the verdict
/// vocabulary must stay grounding-only, and the verdict-ingest boundary must reject a "boundary"
/// or "completeness" verdict as damage rather than accept it.
/// </summary>
public sealed class CriticUnchangedGuardTests
{
    [Fact]
    public void VerdictVocabulary_IsGroundingOnly_Unchanged()
    {
        var values = Enum.GetNames<VerificationVerdict>().OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "Grounded", "Hallucinated", "Manual", "Partial" },
            values);
    }

    [Theory]
    [InlineData("boundary")]
    [InlineData("completeness")]
    [InlineData("incomplete")]
    public void VerdictIngestor_RejectsCompletenessVerdict_AsDamage(string verdict)
    {
        var response = $"{{\"verdict\":\"{verdict}\",\"score\":0.5}}";

        var result = VerdictIngestor.Classify(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(VerdictIngestOutcome.ParseFailure, result.Outcome);
    }

    [Fact]
    public void VerdictIngestor_StillAcceptsGroundingVerdicts()
    {
        var result = VerdictIngestor.Classify("{\"verdict\":\"grounded\",\"score\":0.95,\"findings\":[]}");

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, result.Result!.Verdict);
    }
}
