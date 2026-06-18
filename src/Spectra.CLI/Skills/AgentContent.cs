namespace Spectra.CLI.Skills;

/// <summary>
/// Provides bundled agent prompt file contents, loaded from embedded .md resources.
/// </summary>
public static class AgentContent
{
    public static readonly Dictionary<string, string> All = SkillResourceLoader.GetAllAgents();

    // skill-pair-merge: spectra-generation and spectra-execution agents merged into their flow skills.
    /// <summary>Spec 055: the context:fork critic subagent skill.</summary>
    public static string CriticAgent => All["spectra-critic.agent.md"];
}
