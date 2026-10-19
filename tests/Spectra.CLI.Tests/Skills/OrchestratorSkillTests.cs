using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 092 T5–T6 (FR-008–FR-015, FR-018, FR-019): the update and criteria skills orchestrate — the main session
/// delegates edits, repairs and extraction to the bundled agents, never reads documents, prompts, tests, staging or
/// verdict files, routes by the agents' returned lines and relays every line to the user. On 309174b the main session
/// did all the work itself.
/// </summary>
public sealed class OrchestratorSkillTests
{
    private static string Update => SkillContent.Update.Replace("\r\n", "\n");

    private static string Criteria => SkillContent.All["spectra-criteria"].Replace("\r\n", "\n");

    private static string Section(string content, string from, string to)
    {
        var start = content.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no {from}");
        var end = content.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"no {to} after {from}");
        return content[start..end];
    }

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    // ---- update: the contracts ----

    [Fact]
    public void UpdateSkill_StatesTheContextBudgetContract_BeforeTheFlow()
    {
        var content = Update;
        var contract = content.IndexOf("## Context budget contract", StringComparison.Ordinal);

        Assert.True(contract >= 0 && contract < content.IndexOf("## Update test cases for a suite", StringComparison.Ordinal));
        var section = Normalised(Section(content, "## Context budget contract", "## Update test cases for a suite"));
        foreach (var item in new[] { "docs", "compiled prompts", "test files", "staging", "verdict files", "Task", "`update-ack`", "`.spectra/progress.json`" })
        {
            Assert.Contains(item, section);
        }
    }

    [Fact]
    public void UpdateSkill_NonStop_NamesTheAgents_AndWritesOnlyProgress()
    {
        var section = Normalised(Section(Update, "## NON-STOP CONTRACT", "## Update test cases for a suite"));

        Assert.Contains("`spectra-updater`", section);
        Assert.Contains("`spectra-repairer`", section);
        Assert.Contains("`spectra-critic`", section);
        Assert.Contains("`Write` only to `.spectra/progress.json`", section);
        Assert.DoesNotContain("updated-<id>.json", section);
        Assert.DoesNotContain("repaired-<id>.json", section);
    }

    // ---- update: Step 5 ----

    [Fact]
    public void UpdateSkill_Step5_DelegatesEachGroupToTheUpdater_AndRelaysEveryLine()
    {
        var step = Section(Update, "**Step 5**", "**Step 5a**");
        var flat = Normalised(step);

        Assert.Contains("`spectra-updater`", flat);
        Assert.Contains("`group: <group>`", flat);
        Assert.Contains("`ids: <id>`", flat);
        Assert.DoesNotMatch(@"spectra ai compile-update-prompt", step);
        Assert.DoesNotMatch(@"spectra ai ingest-update", step);
        Assert.DoesNotContain("with the Write tool", step);
        Assert.Contains("Report every line", flat);
        Assert.Contains("acknowledge", flat);
        Assert.Contains("edit again", flat);
        Assert.Contains("not redone", flat);
    }

    [Fact]
    public void UpdateSkill_Step5_AStaleTriage_IsReTriaged_NotTheEndOfTheSuite()
    {
        // FR-022 (analysis U1): TRIAGE_STALE kept its 088 behaviour — re-run Step 1, then continue.
        var flat = Normalised(Section(Update, "**Step 5**", "**Step 5a**"));

        Assert.Contains("`stop: triage is stale`", flat);
        Assert.Contains("re-run Step 1", flat);
    }

    // ---- update: Step 5a ----

    [Fact]
    public void UpdateSkill_Step5a_RoutesByTheCriticsLine_AndDelegatesRepairs()
    {
        var step = Section(Update, "**Step 5a**", "**Step 6**");
        var flat = Normalised(step);

        Assert.Contains("`spectra-critic`", flat);
        Assert.Contains("`spectra-repairer`", flat);
        Assert.Contains("final line", flat);
        Assert.DoesNotMatch(@"spectra ai compile-repair-prompt", step);
        Assert.DoesNotMatch(@"spectra ai ingest-update", step);
        Assert.DoesNotContain("read each test's verdict file", flat);
        Assert.Contains("never read the verdict file", flat);
        Assert.Contains("unverified", flat);
        Assert.Contains("--all --repaired --repair-attempts 1 --ids", flat);
        Assert.Contains("repair failed: <error_code>", flat);
        Assert.Contains("repair outcome unknown", flat);
    }

    // ---- update: all suites, Step 7 ----

    [Fact]
    public void UpdateSkill_AllSuites_OneAtATime_AndTheReportComesFromTheReturnedLines()
    {
        // Spec 101 F2: "## Exit codes" moved to reference.md; "## Stop" now follows.
        Assert.Contains("one suite at a time", Normalised(Section(Update, "## Update all suites", "## Stop")));
        var report = Normalised(Section(Update, "**Step 7**", "## Update all suites"));
        Assert.Contains("returned lines", report);
        Assert.Contains("baseline", report);
        Assert.Contains("warning", report);
    }

    // ---- criteria: Step 2, Step 3 ----

    [Fact]
    public void CriteriaSkill_Step2_DelegatesEachDocumentToTheExtractor_AndRelaysEveryLine()
    {
        var step = Section(Criteria, "### Step 2", "### Step 3");
        var flat = Normalised(step);

        Assert.Contains("`spectra-extractor`", flat);
        Assert.Contains("`doc: <path>`", flat);
        Assert.Contains("`component: <component>`", flat);
        Assert.DoesNotMatch(@"spectra ai compile-extraction-prompt", step);
        Assert.DoesNotMatch(@"spectra ai ingest-criteria", step);
        Assert.Contains("Report every line", flat);
    }

    [Fact]
    public void CriteriaSkill_Step3_SummarisesFromTheReturnedLines_WithTheFullChangeset()
    {
        var flat = Normalised(Section(Criteria, "### Step 3", "## Import acceptance criteria"));

        Assert.Contains("returned lines", flat);
        Assert.Contains("still referenced by", flat);
        Assert.Contains("Warning:", flat);
        Assert.Contains("failed", flat);
    }

    // ---- guards (pass on 309174b and after) ----

    [Fact]
    public void CriteriaSkill_Step1_And_TheImportAndListFlows_StayInTheMainSession()
    {
        var content = Criteria;

        Assert.Contains("spectra docs retire --doc {path} --dry-run", content);
        Assert.Contains("spectra docs apply-move --from {from} --to {path}", content);
        Assert.Contains("spectra ai analyze --import-criteria", content);
        Assert.Contains("spectra ai analyze --list-criteria", content);
    }
}
