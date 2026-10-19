using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 093 T4–T5 (FR-006–FR-015): the generation skill orchestrates — analysis, each batch and the repairs run in
/// subagents; tests are routed by the verdict the CLI classified (<c>ingest-verdict --all --ids</c> <c>results</c>), never
/// by the critic's own line; a test the critic could not judge is kept unverified (decision C1); the NON-STOP contract and
/// the written==0 guard stay verbatim. The update skill routes the same way, and in both skills every written line is
/// re-verified or listed unverified. On 20ac624 the main session did the work and read verdict files.
/// </summary>
public sealed class GenerateOrchestratorTests
{
    private static string Generate => SkillContent.All["spectra-generate"].Replace("\r\n", "\n");

    private static string Update => SkillContent.Update.Replace("\r\n", "\n");

    private static string Section(string content, string from, string to)
    {
        var start = content.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no {from}");
        var end = content.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"no {to} after {from}");
        return content[start..end];
    }

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    /// <summary>The main generation flow — everything before the from-description flow.</summary>
    private static string MainFlow => Section(Generate, "## Main generation flow", "## When the user wants to create a specific test case");

    // ---- generate: contract ----

    [Fact]
    public void GenerateSkill_StatesTheContextBudgetContract_BeforeTheMainFlow()
    {
        var content = Generate;
        var contract = content.IndexOf("## Context budget contract", StringComparison.Ordinal);

        Assert.True(contract >= 0 && contract < content.IndexOf("## Main generation flow", StringComparison.Ordinal));
        var section = Normalised(Section(content, "## Context budget contract", "## Main generation flow"));
        foreach (var item in new[] { "docs", "compiled prompts", "test files", "generated", "verdict files", "Task", "`.spectra/progress.json`" })
        {
            Assert.Contains(item, section);
        }
    }

    [Fact]
    public void GenerateSkill_MainFlow_DoesNoAnalysisGenerationOrRepairWorkItself()
    {
        var flow = MainFlow;

        Assert.DoesNotMatch(@"spectra ai compile-analysis-prompt", flow);
        Assert.DoesNotMatch(@"spectra ai compile-prompt --suite \{suite\} --count", flow);
        Assert.DoesNotMatch(@"spectra ai compile-repair-batch", flow);
        Assert.DoesNotMatch(@"spectra ai ingest-tests", flow);
        Assert.DoesNotMatch(@"spectra ai ingest-analysis", flow);
        Assert.DoesNotContain("Read .spectra/verdicts", flow);
        Assert.DoesNotContain("Read `test-cases/{suite}/{id}.md` directly", Generate);
    }

    [Fact]
    public void GenerateSkill_DelegatesAnalysisBatchesAndRepairs()
    {
        var flow = Normalised(MainFlow);

        Assert.Contains("`spectra-analyst`", flow);
        Assert.Contains("`spectra-generator`", flow);
        Assert.Contains("`spectra-repairer`", flow);
        // Spec 094: the generator gets a batch id of the plan (and a count only for an explicit total).
        Assert.Contains("`batch: <id>`", flow);
        Assert.Contains("`count: <k>`", flow);
    }

    // ---- generate: routing, unverified, drops ----

    [Fact]
    public void GenerateSkill_RoutesByTheClisPerTestVerdicts()
    {
        var flow = Normalised(MainFlow);

        Assert.Contains("spectra ai ingest-verdict --suite {suite} --all --ids", flow);
        Assert.Contains("`results`", flow);
        // Spec 101 F3: the generator relays the CLI's verdict per test; the critic's own line is only compared.
        Assert.Contains("never read the verdict file", flow);
        Assert.Contains("unverified", flow);
    }

    [Fact]
    public void GenerateSkill_ATestTheCriticCouldNotJudge_IsKeptUnverified_NeverRegenerated()
    {
        // Decision C1 (2026-10-09).
        var flow = Normalised(MainFlow);

        Assert.DoesNotContain("Regenerate that single test", flow);
        Assert.Contains("the critic once more", flow);
        Assert.Contains("Unverified (critic could not judge)", flow);
        Assert.Contains("never regenerated or deleted", flow);
    }

    [Fact]
    public void GenerateSkill_DropsOnlyWhenTheCriticsLineAgrees()
    {
        // Analysis U1.
        Assert.Contains("only when the critic's line agrees or there is no line", Normalised(MainFlow));
    }

    [Fact]
    public void GenerateSkill_ResumesFromTheGroundingAudit()
    {
        var flow = Normalised(MainFlow);
        // Spec 101 F2: the resume procedure moved to the skill's reference.md (read when a run is resumed).
        var resume = Normalised(SkillReferenceContent.Generate);

        Assert.Contains("audit-grounding", flow);
        Assert.Contains("Resume an interrupted run", flow);
        Assert.Contains("`action_needed: repair`", resume);
        Assert.DoesNotContain("re-run `compile-repair-batch`", resume);
    }

    [Fact]
    public void GenerateSkill_ReportsEveryTestByIdAndTitle()
    {
        Assert.Contains("by id and title", Normalised(Section(Generate, "### Step 9", "## When the user wants to create a specific test case")));
    }

    // ---- generate: guards (pass before and after) ----

    [Fact]
    public void GenerateSkill_NonStopContract_AndTheWrittenZeroGuard_StayVerbatim()
    {
        var content = Generate;

        Assert.Contains("## NON-STOP CONTRACT — Steps 7–9", content);
        Assert.Contains("- **(a) Shell loops over per-test verbs**", content);
        Assert.Contains("- **(b) Piping spectra output to any interpreter**", content);
        Assert.Contains("- **(c) Manual `.md` file editing**", content);
        Assert.Contains("- **(d) Verdict / grounding field rewrites by hand**", content);
        Assert.Contains("- **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b.", content);
        Assert.Contains("GROUNDING-INGEST ANOMALY: `ingest-grounding --all` returned `written: 0`", content);
    }

    [Fact]
    public void FromDescription_StillGeneratesInTheSession_AndUsesTheStep8Critic()
    {
        // Spec 101 F2: the flow is a numbered list followed by the routing table; its detail is in reference.md.
        var section = Section(Generate, "## When the user wants to create a specific test case", "## How to choose between generation flows");

        Assert.Contains("spectra ai compile-prompt --suite {suite} --from-description", section);
        Assert.Contains("Step 8", Section(section, "\n4. ", "\n5. "));
    }

    // ---- update ----

    [Fact]
    public void UpdateSkill_Step5a_RoutesByTheClisPerTestVerdicts()
    {
        var step = Normalised(Section(Update, "**Step 5a**", "**Step 6**"));

        Assert.Contains("spectra ai ingest-verdict --suite <suite> --all --ids", step);
        Assert.Contains("`results`", step);
        // Spec 101 F3: the updater relays the CLI's verdict per test; on the fallback the critic's line is only compared.
        Assert.Contains("never read the verdict file", step);
        Assert.Contains("disagree", step);
        Assert.Contains("the critic once more", step);
    }

    // ---- both: every written line ----

    [Fact]
    public void BothSkills_ReVerifyEveryWrittenLine_OrListItUnverified()
    {
        // FR-014 (092 review finding C2).
        Assert.Contains("every `written` line", Normalised(Section(Update, "**Step 5**", "**Step 6**")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("every `written` line", Normalised(MainFlow), StringComparison.OrdinalIgnoreCase);
    }
}
