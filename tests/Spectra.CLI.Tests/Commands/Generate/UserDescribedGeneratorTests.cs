using Spectra.CLI.Commands.Generate;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Tests for UserDescribedGenerator.BuildPrompt — verifies prompt structure
/// without invoking the AI agent. Covers spec 033 FR-011, FR-012, FR-016.
/// </summary>
public class UserDescribedGeneratorTests
{
    private const string Description = "expired session redirects to login";
    private const string Suite = "checkout";
    private static readonly string[] ExistingIds = ["TC-001", "TC-002"];

    [Fact]
    public void BuildPrompt_WithoutContext_DoesNotIncludeReferenceSection()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(Description, null, Suite, ExistingIds);

        Assert.DoesNotContain("Reference Documentation", prompt);
        Assert.DoesNotContain("Related Acceptance Criteria", prompt);
    }

    [Fact]
    public void BuildPrompt_WithDocContext_IncludesReferenceDocumentationHeader()
    {
        var docContext = "### Checkout Flow (docs/checkout.md)\n\nUser navigates to /checkout...";

        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds, documentContext: docContext);

        Assert.Contains("## Reference Documentation (for formatting context only)", prompt);
        Assert.Contains("Checkout Flow", prompt);
        Assert.Contains("the user's description is the source of truth", prompt);
    }

    [Fact]
    public void BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaHeader()
    {
        var criteriaContext = "- **AC-001** [MUST] Expired sessions redirect to /login";

        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds, criteriaContext: criteriaContext);

        Assert.Contains("## Related Acceptance Criteria", prompt);
        Assert.Contains("AC-001", prompt);
        Assert.Contains("criteria` frontmatter field", prompt);
    }

    [Fact]
    public void BuildPrompt_WithBothContexts_IncludesBoth()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds,
            documentContext: "doc body",
            criteriaContext: "- **AC-001** something");

        Assert.Contains("Reference Documentation", prompt);
        Assert.Contains("Related Acceptance Criteria", prompt);
    }

    [Fact]
    public void BuildPrompt_WithDocContext_StatesUserDescriptionIsSourceOfTruth()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds, documentContext: "doc body");

        Assert.Contains("source of truth", prompt);
    }

    [Fact]
    public void BuildPrompt_AlwaysIncludesUserDescription()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(Description, null, Suite, ExistingIds);

        Assert.Contains(Description, prompt);
    }

    [Fact]
    public void BuildPrompt_AlwaysAvoidsExistingIds()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(Description, null, Suite, ExistingIds);

        Assert.Contains("TC-001", prompt);
        Assert.Contains("TC-002", prompt);
        Assert.Contains("do not duplicate", prompt);
    }

    [Fact]
    public void BuildPrompt_WithExtraContext_IncludesContextLine()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, "checkout page", Suite, ExistingIds);

        Assert.Contains("Additional context: checkout page", prompt);
    }

    [Fact]
    public void BuildPrompt_WithEmptyDocContext_DoesNotIncludeReferenceSection()
    {
        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds, documentContext: "   ");

        Assert.DoesNotContain("Reference Documentation", prompt);
    }
}
