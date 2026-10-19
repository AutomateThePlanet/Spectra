using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 093 T3 (FR-001–FR-005, FR-013): the analyst and generator run forked with the session's model and only the tools
/// they need, follow the NON-STOP contract, never ask the user, and end with only their return lines; the repairer
/// reports every ingested file by id. On 20ac624 the two agents didn't exist and the repairer reported per input id.
/// </summary>
public sealed class GenerateAgentTests
{
    private const string Analyst = "spectra-analyst.agent.md";
    private const string Generator = "spectra-generator.agent.md";

    public static TheoryData<string> NewAgents => new() { Analyst, Generator };

    private static string Agent(string file)
    {
        Assert.True(AgentContent.All.TryGetValue(file, out var content), $"{file} is not bundled");
        return content!.Replace("\r\n", "\n");
    }

    private static string Body(string content) => Normalised(content[(content.IndexOf("\n---\n", 4, StringComparison.Ordinal) + 5)..]);

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    [Theory]
    [MemberData(nameof(NewAgents))]
    public void TheAgent_RunsForked_WithTheSessionsModel_AndOnlyTheToolsItNeeds(string file)
    {
        var content = Agent(file);
        var frontmatter = Regex.Match(content, @"\A---\n(.*?)\n---\n", RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains($"name: {file.Replace(".agent.md", "")}", frontmatter);
        // Spec 101 F3: the generator starts spectra-critic for the tests it created.
        Assert.Equal(file == Generator ? "tools: [Bash, Read, Write, Agent]" : "tools: [Bash, Read, Write]",
            frontmatter.Split('\n').Single(l => l.StartsWith("tools:", StringComparison.Ordinal)));
        Assert.Contains("model: inherit", frontmatter);
        // Spec 101 F4: skill fields Claude Code ignores on a subagent are gone; the description says who it's for.
        Assert.DoesNotContain("disable-model-invocation", frontmatter);
        Assert.DoesNotContain("context:", frontmatter);
        Assert.Contains("description: Only for use by the spectra-generate skill", frontmatter);
    }

    [Theory]
    [MemberData(nameof(NewAgents))]
    public void TheAgent_NeverAsks_NeverDelegates_NeverEditsATest_AndFollowsNonStop(string file)
    {
        var body = Body(Agent(file));

        Assert.Contains("Never ask the user", body);
        Assert.Contains("Never start a subagent", body);
        Assert.Contains("Never edit a test's `.md` file", body);
        Assert.Contains("NON-STOP", body);
        Assert.Contains("no shell loops", body);
        Assert.Contains("interpreter", body);
        Assert.Contains("`stop: <", body);
        Assert.Contains("## Your final message", Agent(file));
        Assert.Contains("nothing else", body);
    }

    [Fact]
    public void TheAnalyst_AnalysesAndProposesBatches()
    {
        var body = Body(Agent(Analyst));

        Assert.Contains("spectra ai compile-analysis-prompt --suite <suite> --doc-suite <doc_suite>", body);
        Assert.Contains("`.spectra/analysis.json`", body);
        Assert.Contains("spectra ai ingest-analysis --suite <suite> --from .spectra/analysis.json", body);
        Assert.Contains("at most 2", body);
        foreach (var line in new[] { "`already_covered: ", "`recommended: ", "`documents_analyzed: ", "`breakdown: ", "`techniques: ", "`boundary_gap: " })
        {
            Assert.Contains(line, body);
        }

        // Spec 094: the analyst copies the CLI's batch lines (the plan) instead of planning batches itself.
        Assert.Contains("`batch <id> | count <k> | <section refs> | <behaviour titles>`", body);
        Assert.Contains("verbatim", body);
        Assert.Contains("`refused: ", body);
        Assert.Contains("`refusal: ", body);
    }

    [Fact]
    public void TheGenerator_GeneratesOneBatch_AndReturnsOneLinePerTest()
    {
        var body = Body(Agent(Generator));

        // Spec 094: the generator compiles one batch of the generation plan.
        Assert.Contains("spectra ai compile-prompt --suite <suite> --batch <batch>", body);
        Assert.Contains("`.spectra/generated.json`", body);
        Assert.Contains("spectra ai ingest-tests <suite> --from .spectra/generated.json", body);
        Assert.Contains("at most 2", body);
        Assert.Contains("`<id> | <title>`", body);
        Assert.Contains("`baseline: ", body);
        Assert.Contains("`warning: ", body);
        Assert.Contains("`failed <error_code>: <first error>`", body);
        Assert.DoesNotContain("compile-critic-prompt", body);
    }

    [Fact]
    public void TheRepairer_ReportsEveryIngestedFile_IncludingLeftovers()
    {
        // FR-013 (092 review finding C1).
        var body = Body(Agent("spectra-repairer.agent.md"));

        Assert.Contains("one line per entry in `results`", body);
        Assert.Contains("including leftover", body);
        Assert.Contains("attributed by id", body);
    }
}
