namespace Spectra.CLI.Skills;

/// <summary>
/// Provides bundled agent prompt file contents, loaded from embedded .md resources.
/// </summary>
public static class AgentContent
{
    public static readonly Dictionary<string, string> All = SkillResourceLoader.GetAllAgents();

    public static string ExecutionAgent => All["spectra-execution.agent.md"];
    public static string GenerationAgent => All["spectra-generation.agent.md"];
}
