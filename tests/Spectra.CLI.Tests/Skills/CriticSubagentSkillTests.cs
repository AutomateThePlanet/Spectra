using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 055 (FR-002/FR-003) — the net-new <c>spectra-critic</c> subagent skill exists, is declared
/// <c>context: fork</c> + <c>disable-model-invocation: true</c> (explicit invocation only), and its
/// instruction restricts the critic's input to the test artifact + source documents (no generator
/// state).
/// </summary>
public sealed class CriticSubagentSkillTests
{
    private static string Skill() => AgentContent.CriticAgent;

    [Fact]
    public void CriticAgent_IsBundled()
    {
        Assert.True(AgentContent.All.ContainsKey("spectra-critic.agent.md"));
        Assert.False(string.IsNullOrWhiteSpace(Skill()));
    }

    [Fact]
    public void CriticAgent_RunsInAFreshContext()
    {
        // Spec 101 F4: a subagent always starts in a fresh context; `context: fork` was a skill field Claude Code
        // ignored on a subagent, so the body says it plainly.
        Assert.Contains("fresh, isolated context", Skill());
        Assert.DoesNotContain("context: fork", Skill());
    }

    [Fact]
    public void CriticAgent_IsOnlyForTheSkills()
    {
        // FR-003, Spec 101 F4: explicit mandatory-step invocation only — said in the description (Claude Code ignores
        // `disable-model-invocation` on a subagent).
        Assert.Contains("description: Only for use by the spectra-generate, spectra-update and spectra-review-flagged skills", Skill());
        Assert.DoesNotContain("disable-model-invocation", Skill());
    }

    [Fact]
    public void CriticAgent_NameAndModelDeclared()
    {
        var skill = Skill();
        Assert.Contains("name: spectra-critic", skill);
        Assert.Contains("model:", skill);
    }

    [Fact]
    public void CriticAgent_RestrictsInputToArtifactAndDocs()
    {
        var skill = Skill();
        // Isolation contract is stated and the model-free choreography commands are referenced.
        Assert.Contains("isolated", skill, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compile-critic-prompt", skill);
        Assert.Contains("ingest-verdict", skill);
        // It must explicitly forbid passing generator state.
        Assert.Contains("MUST NOT", skill);
    }

    [Fact]
    public void CriticAgent_RequiresBothVerdictAndScore()
    {
        // The skill must instruct the model to always render verdict + score (damage fails loud).
        var skill = Skill();
        Assert.Contains("verdict", skill);
        Assert.Contains("score", skill);
    }
}
