using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 102 (F3, F4, F9; FR-009–FR-013, FR-018; research R4, R5, R9; contracts C3–C5).
/// <list type="bullet">
/// <item>F3: the repairer runs the critic only for <c>written</c> repairs; an <c>unchanged</c> repair keeps its verdict,
/// which comes back from the same <c>ingest-verdict --all --ids</c> call, and the skills' fallback starts the critic for
/// <c>written</c> ids only.</item>
/// <item>F4: every result line a work agent documents starts, after <c>&lt;id&gt; | </c>, with a fixed keyword; the
/// generator's title line is <c>&lt;id&gt; | created: &lt;title&gt;</c>.</item>
/// <item>F9: the subagent tool is called <c>Agent</c> everywhere (Claude Code renamed <c>Task</c> in v2.1.63).</item>
/// </list>
/// On 26bfe88 the repairer re-ran the critic for unchanged repairs, the title line was <c>&lt;id&gt; | &lt;title&gt;</c>,
/// and the skills said "Task".
/// </summary>
public sealed class ReviewFixLineAndToolTests
{
    private static string Lf(string text) => text.Replace("\r\n", "\n");

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    private static string Agent(string file) => Lf(AgentContent.All[file]);

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' not found");
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to >= 0, $"'{end}' not found after '{start}'");
        return text[from..to];
    }

    private static string FinalMessage(string agent) => agent[agent.IndexOf("## Your final message", StringComparison.Ordinal)..];

    [Fact]
    public void TheRepairer_RunsTheCritic_OnlyForWrittenRepairs()
    {
        var repairer = Agent("spectra-repairer.agent.md");
        var heading = Normalised(Between(repairer, "**5. Verify with the critic**", "`spectra-critic` subagent with the Agent tool"));

        Assert.Contains("your final `written` lines", heading);
        Assert.DoesNotContain("`written` and `unchanged` lines", heading);
        Assert.Contains("`unchanged` ids", heading);
        Assert.Contains("no critic run", heading);
        Assert.Contains("one verdict line per `written` / `unchanged` test", Normalised(FinalMessage(repairer)));
    }

    [Fact]
    public void TheGenerateSkill_RoutesAnUnchangedRepair_ByItsExistingVerdict()
    {
        var skill = Normalised(Lf(SkillContent.Generate));

        Assert.Contains("verify each `written` repair (an `unchanged` one keeps its verdict)", skill);
        Assert.DoesNotContain("each repair that landed", skill);
        Assert.Contains("one without a verdict line, or `critic: not run`, goes through the fallback", skill);
        Assert.Contains("the critic for `written` ids only, `unchanged` ids straight to its ingest call", skill);
        Assert.DoesNotContain("re-critiqued", skill);
    }

    [Fact]
    public void TheUpdateSkill_RoutesAnUnchangedRepair_ByItsExistingVerdict()
    {
        var skill = Normalised(Lf(SkillContent.Update));

        Assert.Contains("re-verify each `written` repair", skill);
        Assert.Contains("keeps the verdict that sent it to repair", skill);
        Assert.Contains("`--ids` names exactly the re-verified tests", skill);
        Assert.Contains("the critic for `written` ids only, `unchanged` ids straight to its ingest call", skill);
        Assert.DoesNotContain("re-critiqued", skill);
    }

    [Fact]
    public void TheGenerator_ReportsACreatedTest_AsCreated()
    {
        var final = FinalMessage(Agent("spectra-generator.agent.md"));

        Assert.Contains("`<id> | created: <title>`", final);
        Assert.Contains("TC-121 | created: ", final);
        Assert.Contains("TC-122 | created: ", final);
        Assert.DoesNotContain("`<id> | <title>`", final);
    }

    // The updater's `written (manual verdict kept)` and `unchanged_pending <BUCKET>: <why>` already start with a keyword.
    private static readonly string[] Keywords =
        ["created: ", "written", "unchanged", "failed ", "verdict ", "critic-disagrees ", "unverified: "];

    [Theory]
    [InlineData("spectra-generator.agent.md")]
    [InlineData("spectra-updater.agent.md")]
    [InlineData("spectra-repairer.agent.md")]
    public void EveryResultLine_StartsWithAKeyword(string file)
    {
        var forms = Regex.Matches(FinalMessage(Agent(file)), @"`<id> \| ([^`]+)`").Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(forms);
        Assert.All(forms, form => Assert.True(Keywords.Any(k => form.StartsWith(k, StringComparison.Ordinal)),
            $"{file}: `<id> | {form}` starts with no keyword"));
    }

    [Fact]
    public void TheGenerateSkill_ReadsCreatedLines()
    {
        var skill = Lf(SkillContent.Generate);

        Assert.Contains("one `<id> | created: <title>` line per created test", skill);
        Assert.DoesNotContain("one `<id> | <title>` line", skill);
        Assert.Contains("`<id> | verdict <verdict> <score>`", skill);
        Assert.Contains("`<id> | critic-disagrees", skill);
        Assert.Contains("`<id> | unverified: <reason>`", skill);
    }

    [Theory]
    [InlineData("spectra-generate")]
    [InlineData("spectra-update")]
    [InlineData("spectra-criteria")]
    [InlineData("spectra-review-flagged")]
    public void TheAuthoringSkills_ListTheAgentTool(string skill)
    {
        var tools = Lf(SkillContent.All[skill]).Split('\n').First(l => l.StartsWith("tools:", StringComparison.Ordinal));

        Assert.Contains("Agent", tools);
        Assert.DoesNotContain("Task", tools);
    }

    public static IEnumerable<object[]> BundledTexts() =>
        SkillContent.All.Select(kv => new object[] { "skill " + kv.Key, kv.Value })
            .Concat(SkillReferenceContent.All.Select(kv => new object[] { "reference " + kv.Key, kv.Value }))
            .Concat(AgentContent.All.Select(kv => new object[] { "agent " + kv.Key, kv.Value }));

    [Theory]
    [MemberData(nameof(BundledTexts))]
    public void NoBundledText_CallsTheSubagentToolTask(string name, string text)
    {
        foreach (var phrase in new[] { "Task tool", "Task call", "Task prompt" })
        {
            Assert.False(text.Contains(phrase, StringComparison.Ordinal), $"{name} says '{phrase}'");
        }
    }
}
