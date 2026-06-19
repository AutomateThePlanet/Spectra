using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.IO;

/// <summary>Tests for Spec 071 new grounding block fields in TestFileWriter.</summary>
public class TestFileWriterGroundingTests
{
    private readonly TestFileWriter _writer = new();

    private static TestCase CreateTest(GroundingMetadata? grounding = null) => new()
    {
        Id = "TC-113",
        Title = "Verify file size conversion",
        Priority = Priority.Medium,
        Steps = ["Step 1", "Step 2"],
        ExpectedResult = "File size is converted correctly",
        FilePath = "suite/TC-113.md",
        Grounding = grounding
    };

    [Fact]
    public void FormatTestCase_Repaired_False_Omits_Repaired_Field()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            Repaired = false
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.DoesNotContain("repaired:", content);
    }

    [Fact]
    public void FormatTestCase_Repaired_True_Emits_Repaired_Field()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.93,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            Repaired = true
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.Contains("repaired: true", content);
    }

    [Fact]
    public void FormatTestCase_FlaggedForReview_False_Omits_Field()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = ["claim"],
            FlaggedForReview = false
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.DoesNotContain("flagged_for_review:", content);
    }

    [Fact]
    public void FormatTestCase_FlaggedForReview_True_Emits_Field()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = ["claim"],
            FlaggedForReview = true
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.Contains("flagged_for_review: true", content);
    }

    [Fact]
    public void FormatTestCase_RepairAttempts_Zero_Omits_Field()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            RepairAttempts = 0
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.DoesNotContain("repair_attempts:", content);
    }

    [Fact]
    public void FormatTestCase_RepairAttempts_Positive_Emits_Field()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.93,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            RepairAttempts = 1
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.Contains("repair_attempts: 1", content);
    }

    [Fact]
    public void FormatTestCase_CondensedFindings_Empty_Omits_Section()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = ["claim"],
            CondensedFindings = []
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.DoesNotContain("condensed_findings:", content);
    }

    [Fact]
    public void FormatTestCase_CondensedFindings_Populated_Emits_YamlList()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = ["claim"],
            FlaggedForReview = true,
            CondensedFindings =
            [
                new CondensedFinding { Element = "Step 3", Reason = "Conversion factor not in docs" },
                new CondensedFinding { Element = "Expected Result", Reason = "Error message not found" }
            ]
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.Contains("condensed_findings:", content);
        Assert.Contains("element: \"Step 3\"", content);
        Assert.Contains("reason: \"Conversion factor not in docs\"", content);
        Assert.Contains("element: \"Expected Result\"", content);
    }

    [Fact]
    public void FormatTestCase_FullFlaggedPartialBlock_ContainsAllNewFields()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.68,
            Generator = "claude-sonnet-4-6",
            Critic = "claude-sonnet-4-6",
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = ["Step 3: value not in docs"],
            FlaggedForReview = true,
            RepairAttempts = 1,
            CondensedFindings = [new CondensedFinding { Element = "Step 3", Reason = "Value not in docs" }]
        };
        var content = _writer.FormatTestCase(CreateTest(grounding));
        Assert.Contains("verdict: partial", content);
        Assert.Contains("flagged_for_review: true", content);
        Assert.Contains("repair_attempts: 1", content);
        Assert.Contains("condensed_findings:", content);
        Assert.DoesNotContain("repaired:", content);
    }
}
