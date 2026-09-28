using Spectra.CLI.Verification;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Verification;

/// <summary>
/// Spec 078 (User Story 4): the reviewer's objection reaches the rewrite alongside the critic's
/// findings, so the fix addresses what the human actually flagged.
/// </summary>
public class RepairPromptReviewerFeedbackTests
{
    private static TestCase Test() => new()
    {
        Id = "TC-700",
        Title = "Guest checkout email receipt",
        Priority = Priority.Medium,
        Steps = ["Open checkout", "Complete the order"],
        ExpectedResult = "A receipt email arrives",
        FilePath = "checkout/TC-700.md"
    };

    private static IReadOnlyList<CriticFinding> Findings() =>
    [
        new()
        {
            Element = "Step 2",
            Claim = "receipt arrives within five minutes",
            Status = FindingStatus.Unverified,
            Reason = "timing absent from documentation"
        }
    ];

    [Fact]
    public void Compile_WithReviewerFeedback_EmitsItAsItsOwnSection()
    {
        var (prompt, refusal) = RepairPromptCompiler.Compile(
            Test(), Findings(), [],
            reviewerFeedback: "Step 2 references a loyalty tier field that does not exist in the checkout UI.");

        Assert.Null(refusal);
        Assert.NotNull(prompt);
        Assert.Contains("## Reviewer feedback", prompt);
        Assert.Contains("loyalty tier", prompt);
        // Framed as authoritative, because it may identify something the critic missed entirely.
        Assert.Contains("authoritative", prompt);
    }

    [Fact]
    public void Compile_WithoutReviewerFeedback_OmitsTheSectionEntirely()
    {
        // The automatic repair path must be byte-identical to before Spec 078.
        var (prompt, refusal) = RepairPromptCompiler.Compile(Test(), Findings(), []);

        Assert.Null(refusal);
        Assert.NotNull(prompt);
        Assert.DoesNotContain("## Reviewer feedback", prompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compile_BlankReviewerFeedback_IsTreatedAsAbsent(string? feedback)
    {
        var (prompt, _) = RepairPromptCompiler.Compile(Test(), Findings(), [], feedback);

        Assert.NotNull(prompt);
        Assert.DoesNotContain("## Reviewer feedback", prompt);
    }

    [Fact]
    public void Compile_MultiLineReviewerFeedback_IsQuotedOnEveryLine()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(
            Test(), Findings(), [], "first observation\nsecond observation");

        Assert.NotNull(prompt);
        Assert.Contains("> first observation", prompt);
        Assert.Contains("> second observation", prompt);
    }

    [Fact]
    public void Compile_StillRefusesWhenThereAreNoNonGroundedFindings()
    {
        // Reviewer feedback does not create something to repair on its own — the existing refusal
        // contract is unchanged.
        var (prompt, refusal) = RepairPromptCompiler.Compile(
            Test(), [], [], reviewerFeedback: "something is wrong");

        Assert.Null(prompt);
        Assert.NotNull(refusal);
    }
}
