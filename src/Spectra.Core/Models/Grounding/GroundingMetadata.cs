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

        if (FlaggedForReview && Verdict != VerificationVerdict.Partial)
            return false;

        if (Repaired && Verdict != VerificationVerdict.Grounded)
            return false;

        if (RepairAttempts < 0)
            return false;

        return true;
    }
}
