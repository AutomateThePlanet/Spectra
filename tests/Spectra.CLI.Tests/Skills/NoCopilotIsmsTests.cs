using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 056 (FR-002 / SC-002) — no Copilot-ism ships in any ported authoring skill: no
/// <c>model: GPT-4o</c> pin, no <c>disable-model-invocation</c>, no unexpanded <c>{{…TOOLS}}</c>
/// placeholder, and no Copilot terminal verb. The critic subagent is deliberately exempt (a
/// <c>context: fork</c> subagent legitimately pins its model and is explicit-only).
/// </summary>
public sealed class NoCopilotIsmsTests
{
    private static readonly string[] Forbidden =
    {
        "model: GPT-4o",
        "disable-model-invocation",
        "_TOOLS}}",                 // any unexpanded {{…TOOLS}} placeholder (template-syntax examples like {{title}} are fine)
        "runInTerminal",
        "awaitTerminal",
        "show preview",
        "browser/openBrowserPage",
    };

    public static IEnumerable<object[]> PortedSkills()
    {
        foreach (var (name, content) in SkillContent.All)
            yield return new object[] { name, content };
    }

    [Theory]
    [MemberData(nameof(PortedSkills))]
    public void PortedSkill_HasNoCopilotIsm(string name, string content)
    {
        foreach (var token in Forbidden)
            Assert.False(content.Contains(token, StringComparison.Ordinal),
                $"Ported skill '{name}' still contains Copilot-ism '{token}'.");
    }

    [Fact]
    public void GenerationAgent_HasNoCopilotIsm()
    {
        // skill-pair-merge: generation agent content merged into spectra-generate skill.
        var content = SkillContent.Generate;
        foreach (var token in Forbidden)
            Assert.False(content.Contains(token, StringComparison.Ordinal),
                $"Generation skill still contains Copilot-ism '{token}'.");
    }

    [Fact]
    public void PortedSkills_ResolveToClaudeCodeTools()
    {
        // Frontmatter tools resolve to the Claude Code tool model (no Copilot tool namespaces).
        var generate = SkillContent.Generate;
        Assert.Contains("Read", generate);
        Assert.DoesNotContain("execute/runInTerminal", generate);
        Assert.DoesNotContain("read/readFile", generate);
    }

    [Fact]
    public void CriticSubagent_IsExempt_KeepsModelPinAndExplicitOnly()
    {
        // The critic is NOT part of the authoring port set — it keeps its subagent frontmatter.
        var critic = AgentContent.CriticAgent;
        Assert.Contains("model: claude-sonnet-4-6", critic);
        Assert.Contains("disable-model-invocation: true", critic);
        Assert.Contains("context: fork", critic);
    }
}
