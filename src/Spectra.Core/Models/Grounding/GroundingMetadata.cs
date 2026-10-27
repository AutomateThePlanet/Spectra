namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Grounding metadata written to test frontmatter after verification.
/// </summary>
public sealed record GroundingMetadata
{
    /// <summary>
    /// Verification verdict: grounded, partial, or hallucinated.
    /// </summary>
    public required VerificationVerdict Verdict { get; init; }

    /// <summary>
    /// Confidence score from critic model (0.0 to 1.0).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Model name that generated the test (e.g., "claude-sonnet-4-5").
    /// </summary>
    public required string Generator { get; init; }

    /// <summary>
    /// Model name that verified the test (e.g., "gemini-2.0-flash").
    /// </summary>
    public required string Critic { get; init; }

    /// <summary>
    /// Timestamp when verification was performed.
    /// </summary>
    public required DateTimeOffset VerifiedAt { get; init; }

    /// <summary>
    /// List of claims that could not be verified against documentation.
    /// Only populated for Partial verdicts.
    /// </summary>
    public IReadOnlyList<string> UnverifiedClaims { get; init; } = [];

    /// <summary>
    /// True when verdict is Partial and the test awaits human review.
    /// Only valid when Verdict == Partial.
    /// </summary>
    public bool FlaggedForReview { get; init; }

    /// <summary>
    /// Number of automatic repair attempts applied (0 = no repair attempted).
    /// </summary>
    public int RepairAttempts { get; init; }

    /// <summary>
    /// True when a repair cycle upgraded the test from partial to grounded.
    /// Only valid when Verdict == Grounded.
    /// </summary>
    public bool Repaired { get; init; }

    /// <summary>
    /// Condensed non-grounded findings for human scanning (element + one-line reason).
    /// Full findings remain in the per-test verdict JSON file.
    /// </summary>
    public IReadOnlyList<CondensedFinding> CondensedFindings { get; init; } = [];

    // --- Provenance: round-tripped from frontmatter and carried forward by every write-back (Spec 103 FR-002) ---

    /// <summary>
    /// How the test came to exist, e.g. "user-described". Set by the generation prompt on the
    /// user-described path; preserved by every later write-back.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Identity that authored a hand-created test. Preserved by every later write-back.
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// Free-text annotation on the grounding block. For <see cref="VerificationVerdict.Manual"/>
    /// tests this is generator provenance; for grounded/partial tests it is the human reviewer's
    /// note (Spec 103 Decision 5). The two meanings never coexist, because a manual test is
    /// ineligible for review.
    /// </summary>
    public string? Note { get; init; }

    // --- Human review record (Spec 103 FR-001) ---

    /// <summary>
    /// Identity of the human who last reviewed this test's critic verdict. A non-null value is the
    /// single signal that the test has been human-reviewed — there is deliberately no companion
    /// boolean (Spec 103 Decision 4).
    /// </summary>
    public string? ReviewedBy { get; init; }

    /// <summary>
    /// When that review happened. Always written together with <see cref="ReviewedBy"/>.
    /// Compared against <see cref="VerifiedAt"/> to decide re-raise suppression (FR-010).
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>
    /// When the re-raise <b>currently being withheld</b> was suppressed, because a newer review
    /// stands (FR-011). This means <i>currently</i> suppressed: it is cleared the moment the test is
    /// re-raised, re-reviewed, or comes back with a non-partial verdict, so it is never a historical
    /// record of a lapsed suppression. Mutually exclusive with <see cref="FlaggedForReview"/>.
    /// Written by the verification pass, never by a reviewer.
    /// </summary>
    public DateTimeOffset? ReraiseSuppressedAt { get; init; }

    /// <summary>
    /// Validates the metadata has valid values.
    /// </summary>
    public bool IsValid()
    {
        if (Score < 0.0 || Score > 1.0)
            return false;

        if (string.IsNullOrWhiteSpace(Generator) || string.IsNullOrWhiteSpace(Critic))
            return false;

        if (Verdict == VerificationVerdict.Partial && UnverifiedClaims.Count == 0)
            return false;

        // Spec 103 FR-008: a confidently-verified test can be flagged by a human, so the marker is
        // no longer partial-only. Hallucinated and Manual remain ineligible (FR-007).
        if (FlaggedForReview
            && Verdict != VerificationVerdict.Partial
            && Verdict != VerificationVerdict.Grounded)
            return false;

        if (Repaired && Verdict != VerificationVerdict.Grounded)
            return false;

        if (RepairAttempts < 0)
            return false;

        // Spec 103: the review record is written as a unit — one half without the other is malformed.
        if ((ReviewedBy is null) != (ReviewedAt is null))
            return false;

        // Spec 103 FR-007: neither verdict enters the review workflow, so a review record on one
        // indicates a hand-edited or corrupt file.
        if (ReviewedBy is not null
            && (Verdict == VerificationVerdict.Hallucinated || Verdict == VerificationVerdict.Manual))
            return false;

        // Spec 103 FR-011: only a review can suppress a re-raise.
        if (ReraiseSuppressedAt is not null && ReviewedBy is null)
            return false;

        // Spec 103 (option (a)): the marker means *currently* suppressed. A flagged test withholds
        // nothing, so the two are mutually exclusive.
        if (ReraiseSuppressedAt is not null && FlaggedForReview)
            return false;

        return true;
    }
}
