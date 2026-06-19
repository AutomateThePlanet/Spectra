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

    [Fact]
    public void IsValid_FlaggedForReviewOnGrounded_ReturnsFalse()
    {
        var grounding = ValidGrounded() with { FlaggedForReview = true };
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
}
