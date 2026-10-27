using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 101 F3 (FR-007, FR-014–FR-019; US4; contracts C3, C4): the generator, updater and repairer run the critic for
/// the tests they wrote (Agent tool), ingest the verdicts with one <c>ingest-verdict --all --ids</c> call and return one
/// verdict line per test, or <c>critic: not run</c>. The skills route by those lines and run the critic themselves only
/// on the fallback; grounding writes, drops and deletes stay in the main session. On 6a4c41a the work agents had no
/// Agent tool and the main session ran the critic once per test.
/// </summary>
public sealed class NestedCriticTests
{
    public static TheoryData<string> WorkAgents => new()
    {
        "spectra-generator.agent.md",
        "spectra-updater.agent.md",
        "spectra-repairer.agent.md",
    };

    private static string Agent(string file) => AgentContent.All[file].Replace("\r\n", "\n");

    private static string Frontmatter(string content) =>
        Regex.Match(content, @"\A---\n(.*?)\n---\n", RegexOptions.Singleline).Groups[1].Value;

    private static string Body(string content) => content[(content.IndexOf("\n---\n", 4, StringComparison.Ordinal) + 5)..];

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    private static string Generate => SkillContent.Generate.Replace("\r\n", "\n");

    private static string Update => SkillContent.Update.Replace("\r\n", "\n");

    [Theory]
    [MemberData(nameof(WorkAgents))]
    public void TheWorkAgent_HasTheAgentTool(string file)
    {
        Assert.Equal("tools: [Bash, Read, Write, Agent]",
            Frontmatter(Agent(file)).Split('\n').Single(l => l.StartsWith("tools:", StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(WorkAgents))]
    public void TheWorkAgent_RunsTheCriticPerTest_ThenOneVerdictIngest_AndReturnsVerdictLines(string file)
    {
        var body = Body(Agent(file));

        Assert.Contains("`spectra-critic`", body);
        Assert.Contains("spectra ai ingest-verdict --suite <suite> --all --ids", body);
        Assert.Contains("once more", body);
        Assert.Contains("| verdict ", body);
        Assert.Contains("| critic-disagrees ", body);
        Assert.Contains("| unverified: ", body);
        Assert.Contains("critic: not run", body);
        Assert.Contains("no final line", Normalised(body));
    }

    [Theory]
    [MemberData(nameof(WorkAgents))]
    public void TheWorkAgent_NeverWritesGrounding_DropsOrDeletes(string file)
    {
        var body = Body(Agent(file));

        Assert.DoesNotMatch(new Regex(@"^\s*spectra (ai ingest-grounding|ai record-drop|delete)\b", RegexOptions.Multiline), body);
    }

    [Fact]
    public void TheUpdater_RunsTheCritic_OnlyWithVerifyYes_AndNeverForAManualVerdict()
    {
        var body = Normalised(Body(Agent("spectra-updater.agent.md")));

        Assert.Contains("verify: yes", body);
        Assert.Contains("verify: no", body);
        Assert.Contains("never for `written (manual verdict kept)`", body);
    }

    [Fact]
    public void TheCriticAgent_AcceptsFullDocs()
    {
        var body = Body(Agent("spectra-critic.agent.md"));

        Assert.Contains("full_docs", body);
        Assert.Contains("--full-docs", body);
    }

    [Fact]
    public void TheGenerateSkill_RoutesByTheVerdictLines_AndFallsBack()
    {
        Assert.Contains("| verdict ", Generate);
        Assert.Contains("critic: not run", Generate);
        Assert.Contains("spectra ai ingest-grounding --suite {suite} --all --ids {kept-grounded ids} --output-format json", Generate);
    }

    [Fact]
    public void TheUpdateSkill_AsksTheReVerifyQuestion_BeforeTheFirstUpdaterCall()
    {
        var text = Normalised(Update);
        var question = text.IndexOf("\"Re-verify the edited tests with the critic now? (recommended)\"", StringComparison.Ordinal);
        var firstUpdater = text.IndexOf("invoke the `spectra-updater` subagent", StringComparison.OrdinalIgnoreCase);

        Assert.True(question >= 0, "the re-verify question is missing");
        Assert.True(firstUpdater >= 0, "no updater invocation found");
        Assert.True(question < firstUpdater, "the re-verify question comes after the first updater call");
        Assert.Contains("verify: yes", text);
        Assert.Contains("critic: not run", text);
    }

    [Fact]
    public void FromDescriptionAndReviewFlagged_StillRunTheCriticInTheMainSession()
    {
        var fromDescription = Generate[Generate.IndexOf("## When the user wants to create a specific test case", StringComparison.Ordinal)..];
        Assert.Contains("critic", fromDescription[..Math.Min(fromDescription.Length, 2500)]);
        Assert.Contains("`spectra-critic` subagent", SkillContent.All["spectra-review-flagged"]);
    }
}
