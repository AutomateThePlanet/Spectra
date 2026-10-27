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

    // --- Spec 103: note fidelity (FR-006, SC-008) and review-record emission (FR-002) ---

    private static GroundingMetadata WithNote(string note) => new()
    {
        Verdict = VerificationVerdict.Partial,
        Score = 0.72,
        Generator = "claude-code-session",
        Critic = "claude-opus-5",
        VerifiedAt = DateTimeOffset.Parse("2026-08-20T09:14:00Z"),
        UnverifiedClaims = ["Step 3: absent"],
        Note = note
    };

    /// <summary>
    /// The adversarial corpus from contracts/grounding-frontmatter.md. Before Spec 103 the writer
    /// did not escape backslash, so a Windows path silently became a tab and a trailing backslash
    /// broke frontmatter parsing outright.
    /// </summary>
    [Theory]
    [InlineData("plain note")]
    [InlineData("has \"an embedded quote\"")]
    [InlineData(@"C:\temp\build")]
    [InlineData(@"ends with a backslash \")]
    [InlineData("colon: space sequence")]
    [InlineData("# leading hash")]
    [InlineData("- leading dash")]
    [InlineData("  leading and trailing whitespace  ")]
    [InlineData("line one\nline two")]
    [InlineData("carriage\rreturn")]
    [InlineData("tab\tseparated")]
    public void Note_RoundTripsByteIdentically(string note)
    {
        var content = _writer.FormatTestCase(CreateTest(WithNote(note)));

        var parsed = new Spectra.Core.Parsing.TestCaseParser().Parse(content, "suite/TC-113.md");

        Assert.True(parsed.IsSuccess, $"frontmatter failed to parse for note: {note}");
        Assert.NotNull(parsed.Value!.Grounding);
        Assert.Equal(note, parsed.Value.Grounding!.Note);
    }

    [Fact]
    public void Note_OverLongString_RoundTrips()
    {
        var note = new string('x', 5000);
        var content = _writer.FormatTestCase(CreateTest(WithNote(note)));
        var parsed = new Spectra.Core.Parsing.TestCaseParser().Parse(content, "suite/TC-113.md");

        Assert.True(parsed.IsSuccess);
        Assert.Equal(note, parsed.Value!.Grounding!.Note);
    }

    [Fact]
    public void ValidateAnnotation_AcceptsEverythingInTheAdversarialCorpus()
    {
        Assert.Null(TestFileWriter.ValidateAnnotation(@"C:\temp\build"));
        Assert.Null(TestFileWriter.ValidateAnnotation("ends with backslash \\"));
        Assert.Null(TestFileWriter.ValidateAnnotation("line\nbreak"));
        Assert.Null(TestFileWriter.ValidateAnnotation("tab\there"));
        Assert.Null(TestFileWriter.ValidateAnnotation(null));
        Assert.Null(TestFileWriter.ValidateAnnotation(""));
    }

    [Fact]
    public void ValidateAnnotation_RejectsUnrepresentableControlCharacter()
    {
        var error = TestFileWriter.ValidateAnnotation("null\0byte");

        Assert.NotNull(error);
        Assert.Contains("control character", error);
    }

    [Fact]
    public void FormatTestCase_ReviewRecord_IsEmitted()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.72,
            Generator = "claude-code-session",
            Critic = "claude-opus-5",
            VerifiedAt = DateTimeOffset.Parse("2026-08-20T09:14:00Z"),
            UnverifiedClaims = ["Step 3: absent"],
            ReviewedBy = "Oleg Kuzmanov",
            ReviewedAt = DateTimeOffset.Parse("2026-08-28T10:15:00Z"),
            Note = "Confirmed against the support runbook."
        };

        var content = _writer.FormatTestCase(CreateTest(grounding));

        Assert.Contains("reviewed_by: Oleg Kuzmanov", content); // plain scalar: the lossless emitter quotes only when needed
        Assert.Contains("reviewed_at: 2026-08-28T10:15:00Z", content);
        Assert.Contains("note: \"Confirmed against the support runbook.\"", content);
    }

    [Fact]
    public void FormatTestCase_NoReviewRecord_EmitsNoneOfTheNewFields()
    {
        // SC-007: a test without a review record must be emitted exactly as before.
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Generator = "claude-code-session",
            Critic = "claude-opus-5",
            VerifiedAt = DateTimeOffset.Parse("2026-08-20T09:14:00Z")
        };

        var content = _writer.FormatTestCase(CreateTest(grounding));

        Assert.DoesNotContain("reviewed_by:", content);
        Assert.DoesNotContain("reviewed_at:", content);
        Assert.DoesNotContain("reraise_suppressed_at:", content);
        Assert.DoesNotContain("source:", content);
        Assert.DoesNotContain("created_by:", content);
        Assert.DoesNotContain("note:", content);
    }

    [Fact]
    public void FormatTestCase_SuppressionMarker_IsEmitted()
    {
        var grounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.70,
            Generator = "claude-code-session",
            Critic = "claude-opus-5",
            VerifiedAt = DateTimeOffset.Parse("2026-08-31T08:00:00Z"),
            UnverifiedClaims = ["Step 3: absent"],
            ReviewedBy = "Oleg Kuzmanov",
            ReviewedAt = DateTimeOffset.Parse("2026-08-28T10:15:00Z"),
            ReraiseSuppressedAt = DateTimeOffset.Parse("2026-08-31T08:00:00Z")
        };

        var content = _writer.FormatTestCase(CreateTest(grounding));

        Assert.Contains("reraise_suppressed_at: 2026-08-31T08:00:00Z", content);
    }
}
