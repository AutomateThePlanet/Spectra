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
    public void CriticAgent_DeclaresForkIsolation()
    {
        Assert.Contains("context: fork", Skill());
    }

    [Fact]
    public void CriticAgent_DisablesAutoInvocation()
    {
        // FR-003: explicit mandatory-step invocation only — never auto-invoked.
        Assert.Contains("disable-model-invocation: true", Skill());
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
