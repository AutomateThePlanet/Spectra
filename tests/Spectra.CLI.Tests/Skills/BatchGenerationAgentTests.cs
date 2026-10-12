using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 094 T9 (FR-014–FR-017): the analyst copies the CLI's batch lines instead of planning batches; the generator
/// takes a batch id and reads no docs for a section batch; the skill passes batch ids, never generates a 0-count batch,
/// offers to re-run the analysis after a stale or oversized batch and returns to Step 4 after it. The 093 guards stay.
/// On 55b541f the analyst planned its own batches and the generator compiled by count and focus.
/// </summary>
public sealed class BatchGenerationAgentTests
{
    private static string Agent(string file)
    {
        Assert.True(AgentContent.All.TryGetValue(file, out var content), $"{file} is not bundled");
        return Normalised(content!);
    }

    private static string Analyst => Agent("spectra-analyst.agent.md");

    private static string Generator => Agent("spectra-generator.agent.md");

    private static string Generate => SkillContent.All["spectra-generate"].Replace("\r\n", "\n");

    private static string MainFlow
    {
        get
        {
            var content = Generate;
            var start = content.IndexOf("## Main generation flow", StringComparison.Ordinal);
            var end = content.IndexOf("## When the user wants to create a specific test case", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return Normalised(content[start..end]);
        }
    }

    private static string Normalised(string text) => Regex.Replace(text.Replace("\r\n", "\n"), @"\s+", " ");

    // ---- analyst ----

    [Fact]
    public void TheAnalyst_CopiesTheCLIsBatchLinesVerbatim()
    {
        var analyst = Analyst;

        Assert.Contains("verbatim", analyst);
        Assert.Contains("`batch <id> | count <k> | <section refs> | <behaviour titles>`", analyst);
        Assert.Contains("`unresolved: <id> <title> — <reason>`", analyst);
    }

    [Fact]
    public void TheAnalyst_NoLongerPlansBatches()
    {
        var analyst = Analyst;

        Assert.DoesNotContain("Plan the batches", analyst);
        Assert.DoesNotContain("at most 8 behaviours", analyst);
        Assert.DoesNotContain("drop a batch", analyst);
        Assert.DoesNotContain("batch <n> | count <k> | focus", analyst);
    }

    // ---- generator ----

    [Fact]
    public void TheGenerator_TakesABatch_AndCompilesWithBatch()
    {
        var generator = Generator;

        Assert.Contains("`batch: <batch>`", generator);
        Assert.Contains("spectra ai compile-prompt --suite <suite> --batch <batch>", generator);
        Assert.DoesNotContain("--count <count> --focus", generator);
    }

    [Fact]
    public void TheGenerator_ReadsNoDocsForASectionBatch_ButDoesForUnsourcedAndCountFallback()
    {
        var generator = Generator;

        Assert.Contains("never Read a document", generator);
        Assert.Contains("`unsourced`", generator);
        Assert.Contains("`count_fallback`", generator);
        Assert.Contains("{\"command\":\"compile-prompt\"", generator);
        Assert.Contains("`warning: ", generator);
    }

    [Fact]
    public void TheGenerator_EndsOnTheBatchRefusals_AndKnowsNoTemplateRefusal()
    {
        var generator = Generator;

        foreach (var code in new[] { "PLAN_STALE", "PROMPT_TOO_LARGE", "PLAN_MISSING", "BATCH_NOT_FOUND" })
        {
            Assert.Contains(code, generator);
        }

        Assert.DoesNotContain("TEMPLATE_OUTDATED", generator);
    }

    // ---- skill ----

    [Fact]
    public void TheSkill_PassesBatchIds_AndCountOnlyForAnExplicitTotal()
    {
        var flow = MainFlow;

        Assert.Contains("`batch: <id>`", flow);
        Assert.Contains("explicit total", flow);
        Assert.DoesNotContain("focus: \"<user focus>; <batch focus>\"", flow);
    }

    [Fact]
    public void TheSkill_NeverGeneratesAZeroCountBatch_AndListsIt()
    {
        var flow = MainFlow;

        Assert.Contains("count 0 after splitting your total", flow);
        Assert.Contains("never invoke the generator for a batch whose count is 0", flow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSkill_OffersToReRunTheAnalysis_AndReturnsToStep4()
    {
        var flow = MainFlow;

        Assert.Contains("PLAN_STALE", flow);
        Assert.Contains("PROMPT_TOO_LARGE", flow);
        Assert.Contains("re-run the analysis", flow);
        Assert.Contains("back to Step 4", flow);
        Assert.Contains("created test ids", flow);
        Assert.Contains("nothing is regenerated automatically", flow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSkill_ReportsEachDistinctTemplateWarningOnce()
    {
        Assert.Contains("each distinct template warning", MainFlow, StringComparison.OrdinalIgnoreCase);
    }

    // ---- guards (pass on 55b541f and after) ----

    [Fact]
    public void The093Guards_StayInTheSkill()
    {
        var content = Generate;

        Assert.Contains("- **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b.", content);
        Assert.Contains("## NON-STOP CONTRACT", content);
        Assert.Contains("spectra ai ingest-verdict --suite {suite} --all --ids", content);
        Assert.Contains("spectra ai compile-prompt --suite {suite} --from-description", content);
    }
}
