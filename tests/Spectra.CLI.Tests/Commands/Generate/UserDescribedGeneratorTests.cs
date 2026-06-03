using Spectra.CLI.Agent;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Commands.Generate;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Models.Testimize;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Tests for UserDescribedGenerator — verifies prompt structure (BuildPrompt) and
/// the from-description criteria-injection contract (GenerateAsync forwards the
/// loaded criteriaContext to the agent so the MANDATORY mapping block activates).
/// Covers spec 033 (FR-011/012/016) and spec 050.
/// </summary>
public class UserDescribedGeneratorTests
{
    private const string Description = "expired session redirects to login";
    private const string Suite = "checkout";
    private static readonly string[] ExistingIds = ["TC-001", "TC-002"];

    // ---------------------------------------------------------------------
    // BuildPrompt tests (prompt structure, no AI invocation)
    // ---------------------------------------------------------------------

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
    public void BuildPrompt_WithCriteriaContext_OmitsLooseAcceptanceCriteriaSection()
    {
        // Spec 050: the loose "## Related Acceptance Criteria" body block was removed.
        // Criteria now reach the model via the MANDATORY block emitted by
        // CopilotGenerationAgent.BuildFullPrompt when criteriaContext is non-empty.
        var criteriaContext = "- **AC-001** [MUST] Expired sessions redirect to /login";

        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds, criteriaContext: criteriaContext);

        Assert.DoesNotContain("## Related Acceptance Criteria", prompt);
        // The criteria text itself is no longer inlined into the hand-built body.
        Assert.DoesNotContain("AC-001", prompt);
    }

    [Fact]
    public void BuildPrompt_WithBothContexts_IncludesDocReferenceOnly()
    {
        // Spec 050: doc reference still appears; criteria block does not.
        var prompt = UserDescribedGenerator.BuildPrompt(
            Description, null, Suite, ExistingIds,
            documentContext: "doc body",
            criteriaContext: "- **AC-001** something");

        Assert.Contains("Reference Documentation", prompt);
        Assert.DoesNotContain("Related Acceptance Criteria", prompt);
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

    // ---------------------------------------------------------------------
    // Spec 050: from-description criteria-injection contract (GenerateAsync)
    // ---------------------------------------------------------------------

    private const string CriteriaContext =
        "- **AC-CHECKOUT-003** [MUST] Guest checkout completes with a single item\n" +
        "- **AC-CHECKOUT-007** [MUST] Shipping address pre-fills from profile";

    [Fact]
    public async Task FromDescription_PassesCriteriaContextToAgent()
    {
        var fake = new FakeAgentRuntime();

        await GenerateWithFakeAsync(fake, criteriaContext: CriteriaContext);

        Assert.Equal(CriteriaContext, fake.CapturedCriteriaContext);
        Assert.False(string.IsNullOrWhiteSpace(fake.CapturedCriteriaContext));
    }

    [Fact]
    public async Task FromDescription_EmitsMandatoryCriteriaBlock()
    {
        var fake = new FakeAgentRuntime();

        await GenerateWithFakeAsync(fake, criteriaContext: CriteriaContext);

        // The captured criteriaContext is the value that activates the MANDATORY
        // block inside GenerationAgent. Feed it through the real prompt builder to
        // prove the from-description flow now triggers that block.
        var fullPrompt = CopilotGenerationAgent.BuildFullPrompt(
            fake.CapturedPrompt!, 1, criteriaContext: fake.CapturedCriteriaContext);

        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", fullPrompt);
        Assert.Contains("You MUST map each test case", fullPrompt);
        Assert.Contains("AC-CHECKOUT-003", fullPrompt);
    }

    [Fact]
    public async Task FromDescription_DoesNotDuplicateCriteriaSection()
    {
        var fake = new FakeAgentRuntime();

        await GenerateWithFakeAsync(fake, criteriaContext: CriteriaContext);

        // The hand-built prompt (what UserDescribedGenerator passes as userPrompt)
        // must NOT carry the loose criteria block; the criteria appear once, via
        // the MANDATORY block that GenerationAgent emits from criteriaContext.
        Assert.DoesNotContain("## Related Acceptance Criteria", fake.CapturedPrompt);
        Assert.DoesNotContain("AC-CHECKOUT-003", fake.CapturedPrompt);
    }

    [Fact]
    public async Task FromDescription_PopulatesCriteriaField_WhenModelMaps()
    {
        var mapped = new[] { "AC-CHECKOUT-003", "AC-CHECKOUT-007" };
        var fake = new FakeAgentRuntime { ResultCriteria = mapped };

        var test = await GenerateWithFakeAsync(fake, criteriaContext: CriteriaContext);

        Assert.NotNull(test);
        Assert.Equal(mapped, test!.Criteria);
    }

    [Fact]
    public async Task FromDescription_VerdictRemainsManual()
    {
        // Even when criteria are injected and mapped, verdict stays manual:
        // population is not critic verification. Spec 050 Decision D3.
        var fake = new FakeAgentRuntime { ResultCriteria = ["AC-CHECKOUT-003"] };

        var test = await GenerateWithFakeAsync(fake, criteriaContext: CriteriaContext);

        Assert.NotNull(test);
        Assert.Equal(VerificationVerdict.Manual, test!.Grounding.Verdict);
        Assert.Equal("user-described", test.Grounding.Critic);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FromDescription_NoCriteria_OmitsBlock(string? emptyCriteria)
    {
        var fake = new FakeAgentRuntime();

        await GenerateWithFakeAsync(fake, criteriaContext: emptyCriteria);

        // Agent receives null/whitespace → GenerationAgent emits no MANDATORY block.
        Assert.True(string.IsNullOrWhiteSpace(fake.CapturedCriteriaContext));

        var fullPrompt = CopilotGenerationAgent.BuildFullPrompt(
            fake.CapturedPrompt!, 1, criteriaContext: fake.CapturedCriteriaContext);
        Assert.DoesNotContain("ACCEPTANCE CRITERIA — MANDATORY", fullPrompt);

        // And the hand-built prompt carries no loose criteria section either.
        Assert.DoesNotContain("## Related Acceptance Criteria", fake.CapturedPrompt);
    }

    // ---------------------------------------------------------------------
    // Test helpers
    // ---------------------------------------------------------------------

    private static async Task<TestCase?> GenerateWithFakeAsync(
        FakeAgentRuntime fake, string? criteriaContext)
    {
        var generator = new UserDescribedGenerator();
        var config = SpectraConfig.Default;

        return await generator.GenerateAsync(
            Description,
            context: null,
            suite: Suite,
            existingIds: ExistingIds,
            config: config,
            currentDir: Path.GetTempPath(),
            testsPath: Path.GetTempPath(),
            onStatus: null,
            ct: default,
            documentContext: null,
            criteriaContext: criteriaContext,
            sourceRefPaths: null,
            agentFactory: (_, _, _, _, _) =>
                Task.FromResult(AgentCreateResult.Succeeded(fake)));
    }

    /// <summary>
    /// Fake IAgentRuntime that captures the prompt and criteriaContext it receives
    /// and returns a single configurable test case.
    /// </summary>
    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        public string? CapturedPrompt { get; private set; }
        public string? CapturedCriteriaContext { get; private set; }
        public IReadOnlyList<string> ResultCriteria { get; init; } = [];

        public string ProviderName => "fake";

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<GenerationResult> GenerateTestsAsync(
            string prompt,
            IReadOnlyList<SourceDocument> documents,
            IReadOnlyList<TestCase> existingTests,
            int requestedCount,
            string? criteriaContext = null,
            TestimizeDataset? testimizeData = null,
            CancellationToken ct = default)
        {
            CapturedPrompt = prompt;
            CapturedCriteriaContext = criteriaContext;

            var test = new TestCase
            {
                Id = "TC-100",
                FilePath = "test-cases/checkout/TC-100.md",
                Title = "Guest checkout — single item",
                Priority = Priority.Medium,
                ExpectedResult = "Order is placed",
                Criteria = ResultCriteria
            };

            return Task.FromResult(new GenerationResult { Tests = [test] });
        }
    }
}
