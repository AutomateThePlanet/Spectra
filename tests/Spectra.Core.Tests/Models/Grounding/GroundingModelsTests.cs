using Spectra.Core.Models.Grounding;

namespace Spectra.Core.Tests.Models.Grounding;

public class GroundingModelsTests
{
    [Fact]
    public void VerificationVerdict_HasExpectedValues()
    {
        Assert.Equal(0, (int)VerificationVerdict.Grounded);
        Assert.Equal(1, (int)VerificationVerdict.Partial);
        Assert.Equal(2, (int)VerificationVerdict.Hallucinated);
    }

    [Fact]
    public void FindingStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)FindingStatus.Grounded);
        Assert.Equal(1, (int)FindingStatus.Unverified);
        Assert.Equal(2, (int)FindingStatus.Hallucinated);
    }

    [Fact]
    public void CriticFinding_CanBeCreated()
    {
        var finding = new CriticFinding
        {
            Element = "Step 1",
            Claim = "User can log in",
            Status = FindingStatus.Grounded,
            Evidence = "Section 2.1: 'Users authenticate via login form'"
        };

        Assert.Equal("Step 1", finding.Element);
        Assert.Equal("User can log in", finding.Claim);
        Assert.Equal(FindingStatus.Grounded, finding.Status);
        Assert.NotNull(finding.Evidence);
        Assert.Null(finding.Reason);
    }

    [Fact]
    public void CriticFinding_UnverifiedHasReason()
    {
        var finding = new CriticFinding
        {
            Element = "Expected Result",
            Claim = "Email arrives within 5 minutes",
            Status = FindingStatus.Unverified,
            Reason = "No time limit specified in documentation"
        };

        Assert.Equal(FindingStatus.Unverified, finding.Status);
        Assert.Null(finding.Evidence);
        Assert.NotNull(finding.Reason);
    }

    [Fact]
    public void GroundingMetadata_GroundedIsValid()
    {
        var metadata = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Generator = "claude-sonnet-4-5",
            Critic = "gemini-2.0-flash",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        Assert.True(metadata.IsValid());
        Assert.Empty(metadata.UnverifiedClaims);
    }

    [Fact]
    public void GroundingMetadata_PartialRequiresUnverifiedClaims()
    {
        var invalidPartial = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-sonnet-4-5",
            Critic = "gemini-2.0-flash",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = [] // Empty - invalid for partial
        };

        Assert.False(invalidPartial.IsValid());

        var validPartial = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-sonnet-4-5",
            Critic = "gemini-2.0-flash",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = ["Step 3: assumes 5 minute email delivery"]
        };

        Assert.True(validPartial.IsValid());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void GroundingMetadata_InvalidScoreIsInvalid(double score)
    {
        var metadata = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = score,
            Generator = "claude-sonnet-4-5",
            Critic = "gemini-2.0-flash",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        Assert.False(metadata.IsValid());
    }

    [Fact]
    public void GroundingMetadata_EmptyGeneratorIsInvalid()
    {
        var metadata = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Generator = "",
            Critic = "gemini-2.0-flash",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        Assert.False(metadata.IsValid());
    }

    [Fact]
    public void GroundingMetadata_EmptyCriticIsInvalid()
    {
        var metadata = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Generator = "claude-sonnet-4-5",
            Critic = "",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        Assert.False(metadata.IsValid());
    }

    [Fact]
    public void VerificationResult_ToMetadata_CreatesValidMetadata()
    {
        var findings = new List<CriticFinding>
        {
            new()
            {
                Element = "Step 1",
                Claim = "Navigate to login",
                Status = FindingStatus.Grounded,
                Evidence = "Section 1.1"
            },
            new()
            {
                Element = "Step 3",
                Claim = "Email within 5 minutes",
                Status = FindingStatus.Unverified,
                Reason = "No time specified"
            }
        };

        var result = new VerificationResult
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.75,
            Findings = findings,
            CriticModel = "gemini-2.0-flash",
            Duration = TimeSpan.FromSeconds(1.5)
        };

        var metadata = result.ToMetadata("claude-sonnet-4-5");

        Assert.Equal(VerificationVerdict.Partial, metadata.Verdict);
        Assert.Equal(0.75, metadata.Score);
        Assert.Equal("claude-sonnet-4-5", metadata.Generator);
        Assert.Equal("gemini-2.0-flash", metadata.Critic);
        Assert.Single(metadata.UnverifiedClaims);
        Assert.Contains("Step 3: No time specified", metadata.UnverifiedClaims);
    }

    [Fact]
    public void VerificationResult_IsSuccess_TrueWhenNoErrors()
    {
        var result = new VerificationResult
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Findings = [],
            CriticModel = "gemini-2.0-flash"
        };

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void VerificationResult_IsSuccess_FalseWhenErrors()
    {
        var result = new VerificationResult
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Findings = [],
            CriticModel = "gemini-2.0-flash",
            Errors = ["Connection timeout"]
        };

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void VerificationResult_Unverified_CreatesErrorResult()
    {
        var result = VerificationResult.Unverified("gemini-2.0-flash", "Connection timeout");

        Assert.Equal(VerificationVerdict.Partial, result.Verdict);
        Assert.Equal(0.0, result.Score);
        Assert.Empty(result.Findings);
        Assert.Equal("gemini-2.0-flash", result.CriticModel);
        Assert.Single(result.Errors);
        Assert.Contains("Connection timeout", result.Errors);
        Assert.False(result.IsSuccess);
    }
}
