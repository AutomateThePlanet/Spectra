using Spectra.Core.Baseline;
using Spectra.Core.Text;

namespace Spectra.Core.Tests.Baseline;

/// <summary>
/// Spec 084 T3 (FR-012–FR-014): the state table. Each row gives the section's text when the baseline was recorded,
/// whether its snapshot exists, the current text, the light threshold, and the expected state and reasons.
/// </summary>
public class ChangeClassifierTests
{
    private const string Thirteen = "alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima mike";
    private const string Twelve = "alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima";

    public static TheoryData<string, string, bool, string, int, RefState, string[]> Rows => new()
    {
        { "unchanged", "Each user gets 10GB.\n", true, "Each user gets 10GB.\n", 12, RefState.Unchanged, [] },
        { "crlf only", "Each user\ngets 10GB.\n", true, "Each user\r\ngets 10GB.\r\n", 12, RefState.Unchanged, [] },
        { "markup only", "Each user gets 10GB.\n", true, "**Each** user gets 10GB.\n", 12, RefState.Cosmetic, [] },
        { "whitespace only", "Each user gets 10GB.\n", true, "Each  user\ngets 10GB.\n", 12, RefState.Cosmetic, [] },
        { "case only (documented residual)", "Click Save.\n", true, "Click SAVE.\n", 12, RefState.Cosmetic, [] },
        { "number", "Each user gets 10GB.\n", true, "Each user gets 50GB.\n", 12, RefState.Load, ["numbers: 10gb→50gb"] },
        { "more than 4 numbers", "1 2 3 4 5\n", true, "6 7 8 9 10\n", 12, RefState.Load, ["numbers: 1,2,3,4,…→6,7,8,9,…"] },
        { "must → should", "Users must log in.\n", true, "Users should log in.\n", 12, RefState.Load, ["modal: must→should"] },
        { "may", "Users may export.\n", true, "Users can export.\n", 12, RefState.Load, ["modal: may→∅"] },
        { "cannot", "Users cannot delete.\n", true, "Users can delete.\n", 12, RefState.Load, ["modal: cannot→∅"] },
        { "required", "A password is required.\n", true, "A password is optional.\n", 12, RefState.Load, ["modal: required→∅"] },
        { "only", "Only admins see it.\n", true, "Admins see it.\n", 12, RefState.Load, ["modal: only→∅"] },
        { "not", "Do not retry.\n", true, "Do retry.\n", 12, RefState.Load, ["modal: not→∅"] },
        { "must → must not", "Users must log in.\n", true, "Users must not log in.\n", 12, RefState.Load, ["modal: ∅→not"] },
        { "13 words over a threshold of 12", "Intro.\n", true, "Intro. " + Thirteen + "\n", 12, RefState.Load, ["words: 13"] },
        { "exactly 12 words", "Intro.\n", true, "Intro. " + Twelve + "\n", 12, RefState.Light, ["words: 12"] },
        { "one plain word", "Click the button.\n", true, "Click the link.\n", 12, RefState.Light, ["words: 1"] },
        { "threshold 0", "Click the button.\n", true, "Click the link.\n", 0, RefState.Load, ["words: 1"] },
        { "snapshot missing", "Click the button.\n", false, "Click the link.\n", 12, RefState.Load, [ChangeClassifier.SnapshotMissing] },
        { "snapshot missing, cosmetic change", "Each user.\n", false, "**Each** user.\n", 12, RefState.Load, [ChangeClassifier.SnapshotMissing] },
        { "over 5000 words", "Intro.\n", true, "Intro. " + string.Join(' ', Enumerable.Repeat("word", 5001)) + "\n", 12, RefState.Load, ["no diff: over 5000 words"] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void RefState_FollowsTheTable(
        string name, string baselineRaw, bool snapshotExists, string currentRaw, int threshold, RefState state, string[] why)
    {
        var verdict = ChangeClassifier.ClassifyRef(
            "docs/a.md#A",
            SourceFingerprint.OfSection(baselineRaw),
            CurrentSection.Of(SourceFingerprint.OfSection(currentRaw), TextNormalizer.Normalize(currentRaw)),
            snapshotExists ? TextNormalizer.Normalize(baselineRaw) : null,
            threshold);

        Assert.True(state == verdict.State, $"{name}: expected {state}, got {verdict.State}");
        Assert.Equal(why, verdict.Why);
    }

    [Theory]
    [InlineData("anchor not found")]
    [InlineData("not found")]
    [InlineData("outside allowed roots")]
    public void MissingSection_IsRemoved_WithTheReason(string reason)
    {
        var verdict = ChangeClassifier.ClassifyRef("docs/a.md#A", SourceFingerprint.OfSection("x"), CurrentSection.Gone(reason), "x", 12);

        Assert.Equal(RefState.Removed, verdict.State);
        Assert.Equal([reason], verdict.Why);
    }

    [Fact]
    public void ChangedRef_CarriesTheWordDiff()
    {
        var verdict = ChangeClassifier.ClassifyRef(
            "r", SourceFingerprint.OfSection("a 10gb b"), CurrentSection.Of(SourceFingerprint.OfSection("a 50gb b"), "a 50gb b"), "a 10gb b", 12);

        Assert.Equal(2, verdict.Diff.Count);
        Assert.Equal(["10gb"], verdict.Diff[0].Words);
        Assert.True(verdict.Diff[1].Added);
    }

    [Theory]
    [InlineData(true, "same", CriterionState.Unchanged)]
    [InlineData(true, "other", CriterionState.Changed)]
    [InlineData(false, null, CriterionState.Removed)]
    public void CriterionState_FollowsTheTable(bool exists, string? current, CriterionState state)
    {
        var baseline = SourceFingerprint.OfCriterion("same");
        var currentFp = current is null ? null : SourceFingerprint.OfCriterion(current);

        Assert.Equal(state, ChangeClassifier.ClassifyCriterion(baseline, exists, currentFp));
    }
}
