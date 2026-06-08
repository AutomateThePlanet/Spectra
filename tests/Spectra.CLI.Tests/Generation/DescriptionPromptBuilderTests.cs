using Spectra.CLI.Generation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 059 (US2) — token-free unit tests for the from-description prompt shaping relocated out
/// of <c>UserDescribedGenerator</c> so it survives the in-process generator's removal. Combined
/// with <see cref="PromptCompiler.Assemble"/>, this is what `compile-prompt --from-description`
/// emits. Deterministic; no model, no I/O.
/// </summary>
public sealed class DescriptionPromptBuilderTests
{
    [Fact]
    public void Build_EmbedsDescriptionContextAndSuite()
    {
        var prompt = DescriptionPromptBuilder.Build(
            description: "Expired coupon is rejected at checkout",
            context: "Cart page, promo flow",
            suite: "checkout",
            existingIds: ["TC-100", "TC-101"]);

        Assert.Contains("Expired coupon is rejected at checkout", prompt);
        Assert.Contains("Cart page, promo flow", prompt);
        Assert.Contains("checkout", prompt);
        Assert.Contains("TC-100", prompt);   // existing IDs surfaced for the "do not duplicate" rule
        Assert.Contains("TC-101", prompt);
    }

    [Fact]
    public void Build_OmitsContextLine_WhenContextNull()
    {
        var prompt = DescriptionPromptBuilder.Build(
            "A thing happens", context: null, suite: "s", existingIds: []);

        Assert.Contains("A thing happens", prompt);
        Assert.DoesNotContain("Additional context:", prompt);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = DescriptionPromptBuilder.Build("desc", "ctx", "suite", ["TC-1"]);
        var b = DescriptionPromptBuilder.Build("desc", "ctx", "suite", ["TC-1"]);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Assemble_FromDescriptionPrompt_WithCriteria_InjectsMandatoryBlock()
    {
        // End-to-end of the from-description compile path: builder → lenient Assemble (count 1).
        var userPrompt = DescriptionPromptBuilder.Build("desc", null, "suite", []);
        var prompt = PromptCompiler.Assemble(userPrompt, requestedCount: 1,
            criteriaContext: "- **AC-CART-001** [MUST] Reject expired coupons");

        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", prompt);
        Assert.Contains("AC-CART-001", prompt);
    }
}
