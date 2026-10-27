using Spectra.CLI.Install;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Install;

/// <summary>
/// Spec 104 (FR-004): the content installed for non-Claude agents is the bundled Claude content transformed at emit
/// time — no Claude-only wording, frontmatter key or install placeholder survives, and every skill is valid Agent
/// Skills frontmatter (<c>name</c> = folder). The bundled source itself is not changed.
/// </summary>
public sealed class NeutralContentTests
{
    [Fact]
    public void EverySkill_IsNeutral_AndNamedAfterItsFolder()
    {
        Assert.Equal(16, SkillContent.All.Count);
        foreach (var (name, content) in SkillContent.All)
        {
            var neutral = NeutralContent.Skill(name, content);
            AssertNeutral(name, neutral);
            Assert.StartsWith($"---\nname: {name}\ndescription: \"", neutral, StringComparison.Ordinal);
            Assert.DoesNotContain("\ntools:", neutral.Split("\n---\n")[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryReference_IsNeutral()
    {
        foreach (var (name, content) in SkillReferenceContent.All)
        {
            AssertNeutral(name, NeutralContent.Text(content));
        }
    }

    [Fact]
    public void EverySubagent_ParsesWithItsToolsAndANeutralBody()
    {
        var agents = AgentContent.All.Select(a => NeutralContent.Agent(a.Key, a.Value)).ToDictionary(a => a.Name);

        Assert.Equal(
            ["spectra-analyst", "spectra-critic", "spectra-extractor", "spectra-generator", "spectra-repairer", "spectra-updater"],
            agents.Keys.Order(StringComparer.Ordinal));
        foreach (var agent in agents.Values)
        {
            AssertNeutral(agent.Name, agent.Description + "\n" + agent.Body);
            Assert.True(agent.Writes, $"{agent.Name} writes files (verdicts, staged results)");
        }

        // The critic keeps today's tools: read-only set + Write for its verdict file; it starts no subagent.
        Assert.Equal(["Read", "Grep", "Glob", "Bash", "Write"], agents["spectra-critic"].Tools);
        Assert.False(agents["spectra-critic"].StartsSubagents);
        Assert.True(agents["spectra-generator"].StartsSubagents);
    }

    [Fact]
    public void BundledClaudeContent_IsNotChanged_ByTheTransform()
    {
        // The Claude source still speaks Claude: the transform happens only at emit time for other agents.
        Assert.Contains("with the Agent tool", SkillContent.Generate, StringComparison.Ordinal);
        Assert.Contains("tools: [", SkillContent.Generate, StringComparison.Ordinal);
    }

    [Fact]
    public void Fragment_IsNeutral_AndNamesEveryProvidersDelegation()
    {
        var fragment = AgentInstaller.FragmentBody();

        AssertNeutral("spectra.instructions.md", fragment);
        foreach (var product in new[] { "GitHub Copilot", "Cursor", "Devin", "Codex CLI", "Gemini CLI", "Antigravity CLI", "Kiro CLI", "OpenCode" })
        {
            Assert.Contains(product, fragment, StringComparison.Ordinal);
        }

        Assert.Contains("spectra-critic", fragment, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a: b", "\"a: b\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData(@"C:\x", "\"C:\\\\x\"")]
    public void Yaml_QuotesAndEscapes(string value, string expected) => Assert.Equal(expected, NeutralContent.Yaml(value));

    private static void AssertNeutral(string what, string text)
    {
        foreach (var token in NeutralContent.ClaudeOnlyTokens)
        {
            Assert.False(text.Contains(token, StringComparison.Ordinal), $"{what} still contains '{token}'");
        }

        Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
    }
}
