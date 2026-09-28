using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 078 FR-023/FR-024 (AC-9): the reviewing skill's guardrail section distinguishes the critic's
/// fields from the human's record, and all four actions are exposed in plain language.
/// </summary>
public class ReviewFlaggedSkillContractTests
{
    private static string Content()
    {
        Assert.True(SkillContent.All.TryGetValue("spectra-review-flagged", out var content),
            "spectra-review-flagged skill is not bundled");
        Assert.False(string.IsNullOrWhiteSpace(content), "spectra-review-flagged skill content is empty");
        return content!;
    }

    [Fact]
    public void Guardrail_NamesVerdictAndScoreAsProtectedOnAcceptAndFeedback()
    {
        var content = Content();

        Assert.Contains("Accept and feedback MUST NOT modify", content);
        Assert.Contains("grounding.verdict", content);
        Assert.Contains("grounding.score", content);
    }

    [Fact]
    public void Guardrail_NamesRetryRepairAsTheSoleActionAllowedToChangeThem()
    {
        var content = Content();

        Assert.Contains("Retry-repair is the only action that changes them", content);
    }

    [Fact]
    public void Guardrail_RequiresAllThreeHumanActionsToWriteTheReviewRecord()
    {
        var content = Content();

        Assert.Contains("grounding.reviewed_by", content);
        Assert.Contains("grounding.reviewed_at", content);
        Assert.Contains("in addition to, never", content);
    }

    [Fact]
    public void Guardrail_StatesTheRecordSurvivesUnrelatedWriteBacks()
    {
        var content = Content();

        Assert.Contains("survives any unrelated grounding write-back", content);
    }

    [Fact]
    public void Guardrail_ForbidsHandEditingTheSuppressionMarker()
    {
        var content = Content();

        Assert.Contains("NEVER hand-edit `reraise_suppressed_at`", content);
    }

    [Fact]
    public void PlainLanguage_ExposesAllFourActions()
    {
        var content = Content();

        Assert.Contains("looks fine, accept it", content);
        Assert.Contains("regenerate this one", content);
        Assert.Contains("remove it", content);
        Assert.Contains("leave a note that", content);
    }

    [Fact]
    public void PlainLanguage_DistinguishesFeedbackFromFeedbackWithFix()
    {
        var content = Content();

        Assert.Contains("--action feedback --note", content);
        Assert.Contains("--fix", content);
        Assert.Contains("fix it", content);
    }

    [Fact]
    public void Skill_StatesFeedbackIsTheOnlyActionValidOnAnUnflaggedTest()
    {
        var content = Content();

        Assert.Contains("only action valid on a test that is **not** currently flagged", content);
        Assert.Contains("NOT_FLAGGED", content);
    }

    [Fact]
    public void Skill_StatesFixDoesNotRunTheRepairItself()
    {
        var content = Content();

        Assert.Contains("does **not** run the repair", content);
        Assert.Contains("next_step", content);
    }

    [Fact]
    public void Skill_DocumentsTheNonInteractiveActionSurface()
    {
        var content = Content();

        Assert.Contains("--action accept", content);
        Assert.Contains("--action delete", content);
        Assert.Contains("--reviewer", content);
        Assert.Contains("--all", content);
    }
}
