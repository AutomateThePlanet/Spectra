using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 095 T5 (FR-009, FR-010): the analyst relays the CLI's <c>covered: &lt;title&gt; (&lt;covered_by&gt;)</c> lines; the
/// generate skill shows them at approval as "already covered — not generated" and in the final report, and reports the
/// "predates per-behaviour coverage" warning with an offer to re-run the analysis. On db20415 none of this existed.
/// </summary>
public sealed class CoveredLinesTextTests
{
    private static string Normalised(string text) => Regex.Replace(text.Replace("\r\n", "\n"), @"\s+", " ");

    private static string Analyst => Normalised(AgentContent.All["spectra-analyst.agent.md"]);

    private static string Generate => SkillContent.All["spectra-generate"].Replace("\r\n", "\n");

    private static string Section(string from, string to)
    {
        var content = Generate;
        var start = content.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no {from}");
        var end = content.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"no {to} after {from}");
        return Normalised(content[start..end]);
    }

    [Fact]
    public void TheAnalyst_RelaysTheCoveredLinesVerbatim()
    {
        Assert.Contains("`covered: <title> (<covered_by>)`", Analyst);
        Assert.Contains("verbatim", Analyst);
    }

    [Fact]
    public void Step4_ShowsTheCoveredBehaviours_AsAlreadyCoveredNotGenerated()
    {
        var step4 = Section("### Step 4", "## After user approves");

        Assert.Contains("Already covered — not generated", step4);
        Assert.Contains("`covered:`", step4);
    }

    [Fact]
    public void Step9_ListsTheCoveredBehaviours()
    {
        var step9 = Section("### Step 9", "## When the user wants to create a specific test case");

        Assert.Contains("Already covered — not generated", step9);
    }

    [Fact]
    public void TheSkill_ReportsThePredatesCoverageWarning_AndOffersToReRunTheAnalysis()
    {
        var flow = Section("## Main generation flow", "## When the user wants to create a specific test case");

        Assert.Contains("predates per-behaviour coverage", flow);
        Assert.Contains("re-run the analysis", flow);
    }

    [Fact]
    public void The093And094Guards_StayInTheSkill()
    {
        var content = Generate;

        Assert.Contains("- **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b.", content);
        Assert.Contains("`batch: <id>`", Normalised(content));
    }
}
