using Spectra.Core.Models.Grounding;

namespace Spectra.Core.Tests.Grounding;

public class GroundingMetadataTests
{
    private static GroundingMetadata ValidGrounded() => new()
    {
        Verdict = VerificationVerdict.Grounded,
        Score = 0.95,
        Generator = "claude-sonnet-4-6",
        Critic = "claude-sonnet-4-6",
        VerifiedAt = DateTimeOffset.UtcNow,
        UnverifiedClaims = []
    };

    private static GroundingMetadata ValidPartial() => new()
    {
        Verdict = VerificationVerdict.Partial,
        Score = 0.72,
        Generator = "claude-sonnet-4-6",
        Critic = "claude-sonnet-4-6",
        VerifiedAt = DateTimeOffset.UtcNow,
        UnverifiedClaims = ["Step 3: value not in docs"]
    };

    // --- New field defaults ---

    [Fact]
    public void NewGroundingMetadata_DefaultFieldsAreFalseOrZeroOrEmpty()
    {
        var grounding = ValidGrounded();
        Assert.False(grounding.FlaggedForReview);
        Assert.Equal(0, grounding.RepairAttempts);
        Assert.False(grounding.Repaired);
        Assert.Empty(grounding.CondensedFindings);
    }

    // --- IsValid() rules for new fields ---

    [Fact]
    public void IsValid_FlaggedForReviewOnPartial_ReturnsTrue()
    {
        var grounding = ValidPartial() with { FlaggedForReview = true };
        Assert.True(grounding.IsValid());
    }

    /// <summary>
    /// Spec 078 FR-008 deliberately relaxed this: a confidently-verified test can now be flagged by
    /// a human through the <c>feedback</c> action, because a confident-but-wrong critic verdict is
    /// exactly the failure mode a human reviewer exists to catch. Before 078 this returned false.
    /// </summary>
    [Fact]
    public void IsValid_FlaggedForReviewOnGrounded_ReturnsTrue()
    {
        var grounding = ValidGrounded() with { FlaggedForReview = true };
        Assert.True(grounding.IsValid());
    }

    [Fact]
    public void IsValid_FlaggedForReviewOnHallucinated_ReturnsFalse()
    {
        // Still ineligible (FR-007) — only Grounded and Partial reach the review workflow.
        var grounding = ValidGrounded() with
        {
            Verdict = VerificationVerdict.Hallucinated,
            FlaggedForReview = true
        };
        Assert.False(grounding.IsValid());
    }

    [Fact]
    public void IsValid_RepairedOnGrounded_ReturnsTrue()
    {
        var grounding = ValidGrounded() with { Repaired = true };
        Assert.True(grounding.IsValid());
    }

    [Fact]
    public void IsValid_RepairedOnPartial_ReturnsFalse()
    {
        var grounding = ValidPartial() with { Repaired = true };
        Assert.False(grounding.IsValid());
    }

    [Fact]
    public void IsValid_NegativeRepairAttempts_ReturnsFalse()
    {
        var grounding = ValidGrounded() with { RepairAttempts = -1 };
        Assert.False(grounding.IsValid());
    }

    [Fact]
    public void IsValid_ZeroRepairAttempts_ReturnsTrue()
    {
        var grounding = ValidGrounded() with { RepairAttempts = 0 };
        Assert.True(grounding.IsValid());
    }

    [Fact]
    public void IsValid_PositiveRepairAttempts_ReturnsTrue()
    {
        var grounding = ValidGrounded() with { RepairAttempts = 1 };
        Assert.True(grounding.IsValid());
    }

    // --- GroundingFrontmatter roundtrip ---

    [Fact]
    public void GroundingFrontmatter_ToMetadata_MapsNewFields()
    {
        var findings = new List<CondensedFindingFrontmatter>
        {
            new() { Element = "Step 3", Reason = "Value not in docs" }
        };

        var frontmatter = new GroundingFrontmatter
        {
            Verdict = "partial",
            Score = 0.72,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = "2026-06-19T10:00:00Z",
            UnverifiedClaims = ["Step 3: value not in docs"],
            FlaggedForReview = true,
            RepairAttempts = 1,
            Repaired = false,
            CondensedFindings = findings
        };

        var metadata = frontmatter.ToMetadata();

        Assert.NotNull(metadata);
        Assert.True(metadata!.FlaggedForReview);
        Assert.Equal(1, metadata.RepairAttempts);
        Assert.False(metadata.Repaired);
        Assert.Single(metadata.CondensedFindings);
        Assert.Equal("Step 3", metadata.CondensedFindings[0].Element);
        Assert.Equal("Value not in docs", metadata.CondensedFindings[0].Reason);
    }

    [Fact]
    public void GroundingFrontmatter_ToMetadata_EmptyCondensedFindings_MapsToEmpty()
    {
        var frontmatter = new GroundingFrontmatter
        {
            Verdict = "grounded",
            Score = 0.95,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = "2026-06-19T10:00:00Z",
            CondensedFindings = []
        };

        var metadata = frontmatter.ToMetadata();
        Assert.NotNull(metadata);
        Assert.Empty(metadata!.CondensedFindings);
    }

    [Fact]
    public void GroundingFrontmatter_ToMetadata_CondensedFindingWithNullElement_Filtered()
    {
        var findings = new List<CondensedFindingFrontmatter>
        {
            new() { Element = null, Reason = "should be filtered" },
            new() { Element = "Step 1", Reason = "kept" }
        };

        var frontmatter = new GroundingFrontmatter
        {
            Verdict = "partial",
            Score = 0.5,
            Generator = "gen",
            Critic = "critic",
            VerifiedAt = "2026-06-19T10:00:00Z",
            UnverifiedClaims = ["claim"],
            CondensedFindings = findings
        };

        var metadata = frontmatter.ToMetadata();
        Assert.NotNull(metadata);
        Assert.Single(metadata!.CondensedFindings);
        Assert.Equal("Step 1", metadata.CondensedFindings[0].Element);
    }

    // --- Spec 078: provenance + human review record round-trip (FR-001, FR-002) ---

    [Fact]
    public void ToMetadata_MapsProvenanceFieldsThatWerePreviouslyDropped()
    {
        var frontmatter = new GroundingFrontmatter
        {
            Verdict = "partial",
            Score = 0.72,
            Generator = "claude-code-session",
            Critic = "claude-opus-5",
            VerifiedAt = "2026-08-20T09:14:00Z",
            UnverifiedClaims = ["Step 3: interval absent from docs"],
            Source = "user-described",
            CreatedBy = "Oleg Kuzmanov",
            Note = "Confirmed against the support runbook."
        };

        var metadata = frontmatter.ToMetadata();

        Assert.NotNull(metadata);
        Assert.Equal("user-described", metadata!.Source);
        Assert.Equal("Oleg Kuzmanov", metadata.CreatedBy);
        Assert.Equal("Confirmed against the support runbook.", metadata.Note);
    }

    [Fact]
    public void ToMetadata_MapsReviewRecord()
    {
        var frontmatter = new GroundingFrontmatter
        {
            Verdict = "partial",
            Score = 0.72,
            Generator = "claude-code-session",
            Critic = "claude-opus-5",
            VerifiedAt = "2026-08-20T09:14:00Z",
            UnverifiedClaims = ["Step 3: interval absent from docs"],
            ReviewedBy = "Oleg Kuzmanov",
            ReviewedAt = "2026-08-28T10:15:00Z",
            ReraiseSuppressedAt = "2026-08-31T08:00:00Z"
        };

        var metadata = frontmatter.ToMetadata();

        Assert.NotNull(metadata);
        Assert.Equal("Oleg Kuzmanov", metadata!.ReviewedBy);
        Assert.Equal(DateTimeOffset.Parse("2026-08-28T10:15:00Z"), metadata.ReviewedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-08-31T08:00:00Z"), metadata.ReraiseSuppressedAt);
    }

    [Fact]
    public void ToMetadata_ManualVerdict_PreservesProvenance()
    {
        // The user-described generation path is instructed to populate exactly these three.
        var frontmatter = new GroundingFrontmatter
        {
            Verdict = "manual",
            Source = "user-described",
            CreatedBy = "Oleg Kuzmanov",
            Note = "No documentation source for this behaviour."
        };

        var metadata = frontmatter.ToMetadata();

        Assert.NotNull(metadata);
        Assert.Equal(VerificationVerdict.Manual, metadata!.Verdict);
        Assert.Equal("user-described", metadata.Source);
        Assert.Equal("Oleg Kuzmanov", metadata.CreatedBy);
        Assert.Equal("No documentation source for this behaviour.", metadata.Note);
    }

    [Fact]
    public void ToMetadata_AbsentReviewFields_MapToNull()
    {
        var metadata = new GroundingFrontmatter
        {
            Verdict = "grounded",
            Score = 0.95,
            Generator = "g",
            Critic = "c",
            VerifiedAt = "2026-08-20T09:14:00Z"
        }.ToMetadata();

        Assert.NotNull(metadata);
        Assert.Null(metadata!.ReviewedBy);
        Assert.Null(metadata.ReviewedAt);
        Assert.Null(metadata.ReraiseSuppressedAt);
        Assert.Null(metadata.Source);
        Assert.Null(metadata.CreatedBy);
        Assert.Null(metadata.Note);
    }

    [Fact]
    public void ToMetadata_UnparseableReviewTimestamp_TreatedAsAbsent()
    {
        var metadata = new GroundingFrontmatter
        {
            Verdict = "grounded",
            Score = 0.95,
            Generator = "g",
            Critic = "c",
            VerifiedAt = "2026-08-20T09:14:00Z",
            ReviewedBy = "Oleg Kuzmanov",
            ReviewedAt = "not-a-timestamp"
        }.ToMetadata();

        Assert.NotNull(metadata);
        Assert.Null(metadata!.ReviewedAt);
    }
}
