using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 092 T3–T4 (FR-001–FR-007): the three orchestration agents run forked with the session's model and only the tools
/// they need, follow the NON-STOP contract, never ask the user, and end with only their return lines; the critic ends
/// with exactly one line. On 309174b the agents didn't exist and the critic had no final-message rule.
/// </summary>
public sealed class OrchestratorAgentTests
{
    private const string Updater = "spectra-updater.agent.md";
    private const string Repairer = "spectra-repairer.agent.md";
    private const string Extractor = "spectra-extractor.agent.md";

    public static TheoryData<string> NewAgents => new() { Updater, Repairer, Extractor };

    private static string Agent(string file)
    {
        Assert.True(AgentContent.All.TryGetValue(file, out var content), $"{file} is not bundled");
        return content!.Replace("\r\n", "\n");
    }

    private static string Frontmatter(string content)
    {
        var match = Regex.Match(content, @"\A---\n(.*?)\n---\n", RegexOptions.Singleline);
        Assert.True(match.Success, "no frontmatter");
        return match.Groups[1].Value;
    }

    private static string Body(string content) => Normalised(content[(content.IndexOf("\n---\n", 4, StringComparison.Ordinal) + 5)..]);

    private static string Normalised(string text) => Regex.Replace(text, @"\s+", " ");

    // ---- T3: frontmatter ----

    [Theory]
    [MemberData(nameof(NewAgents))]
    public void TheAgent_RunsForked_WithTheSessionsModel_AndOnlyTheToolsItNeeds(string file)
    {
        var frontmatter = Frontmatter(Agent(file));

        Assert.Contains($"name: {file.Replace(".agent.md", "")}", frontmatter);
        var tools = frontmatter.Split('\n').Single(l => l.StartsWith("tools:", StringComparison.Ordinal));
        // Spec 101 F3: the updater and the repairer start spectra-critic for the tests they wrote.
        Assert.Equal(file == Extractor ? "tools: [Bash, Read, Write]" : "tools: [Bash, Read, Write, Agent]", tools);
        Assert.Contains("model: inherit", frontmatter);
        // Spec 101 F4: skill fields Claude Code ignores on a subagent are gone; the description says who it's for.
        Assert.DoesNotContain("disable-model-invocation", frontmatter);
        Assert.DoesNotContain("context:", frontmatter);
        Assert.Contains("description: Only for use by the ", frontmatter);
        Assert.DoesNotContain("Task", tools);
        Assert.DoesNotContain("Edit", tools);
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

    // ---- T3: return contracts ----

    [Fact]
    public void TheUpdater_DoesTheEditStep_AndReturnsOneLinePerResult()
    {
        var body = Body(Agent(Updater));

        Assert.Contains("spectra ai compile-update-prompt --suite <suite> --group <group>", body);
        Assert.Contains("spectra ai compile-update-prompt --suite <suite> --test-id <id>", body);
        Assert.Contains("PROMPT_TOO_LARGE", body);
        Assert.Contains("`warning: <group>: too large for one prompt — edited test by test`", body); // T020 relay
        Assert.Contains("`.spectra/updates/<suite>/updated-<id>.json`", body);
        Assert.Contains("spectra ai ingest-update <suite> --all", body);
        Assert.Contains("at most twice", body);
        Assert.Contains("`<id> | written`", body);
        Assert.Contains("(manual verdict kept)", body);
        Assert.Contains("`<id> | unchanged`", body);
        Assert.Contains("`<id> | unchanged_pending <BUCKET>: <why>`", body);
        Assert.Contains("`<id> | failed <error_code>: <first error>`", body);
        Assert.Contains("baseline: created=", body);
        Assert.Contains("`warning: ", body);
        Assert.DoesNotContain("spectra ai update-ack", body);
        Assert.DoesNotContain("compile-critic-prompt", body);
    }

    [Fact]
    public void TheRepairer_PatchesWhatTheCriticFlagged_AndReturnsOneLinePerId()
    {
        var body = Body(Agent(Repairer));

        Assert.Contains("spectra ai compile-repair-prompt --suite <suite> --test <id>", body);
        Assert.Contains("`.spectra/repairs/<suite>/repaired-<id>.json`", body);
        Assert.Contains("--all --keep-baseline", body);
        Assert.Contains("at most twice", body);
        Assert.Contains("`<id> | written`", body);
        Assert.Contains("`<id> | unchanged`", body);
        Assert.Contains("`<id> | failed <error_code>: <first error>`", body);
        Assert.DoesNotContain("ingest-grounding", body);
        Assert.DoesNotContain("compile-critic-prompt", body);
    }

    [Fact]
    public void TheExtractor_ExtractsOneDocument_AndReturnsTheChangesetVerbatim()
    {
        var body = Body(Agent(Extractor));

        Assert.Contains("spectra ai compile-extraction-prompt --doc", body);
        Assert.Contains("`.spectra/criteria.json`", body);
        Assert.Contains("spectra ai ingest-criteria", body);
        Assert.Contains("LOCK_TIMEOUT", body);
        Assert.Contains("INVALID_CRITERION_ID", body);
        foreach (var outcome in new[] { "`<doc> | extracted`", "`<doc> | unchanged`", "`<doc> | rejected`", "`<doc> | failed <code>`" })
        {
            Assert.Contains(outcome, body);
        }

        Assert.Contains("verbatim", body);
        Assert.Contains("Criteria changeset for", body);
        Assert.Contains("still referenced by", body);
        Assert.Contains("No changes.", body);
        Assert.Contains("Warning: criterion id", body);
        Assert.DoesNotContain("docs retire", body);
        Assert.DoesNotContain("docs apply-move", body);
        Assert.DoesNotContain("docs migrate-criteria", body);
    }

    // ---- T4: the critic's final line ----

    [Fact]
    public void TheCritic_EndsWithExactlyOneLine()
    {
        var critic = AgentContent.All["spectra-critic.agent.md"].Replace("\r\n", "\n");
        var body = Normalised(critic);

        Assert.Contains("## Your final message", critic);
        Assert.Contains("exactly one line", body);
        Assert.Contains("`<id> | <verdict> <score>`", body);
        Assert.Contains("`<id> | failed: <reason>`", body);
    }
}
