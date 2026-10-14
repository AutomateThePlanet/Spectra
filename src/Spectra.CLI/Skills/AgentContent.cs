namespace Spectra.CLI.Skills;

/// <summary>
/// Provides bundled agent prompt file contents, loaded from embedded .md resources.
/// </summary>
public static class AgentContent
{
    /// <summary>Spec 097 (FR-022): the critic agent's model placeholder, filled at install from <c>ai.critic.model</c>.</summary>
    public const string CriticModelPlaceholder = "{{CRITIC_MODEL}}";

    private const string CriticFile = "spectra-critic.agent.md";

    /// <summary>The bundled agents as shipped; the critic still holds <see cref="CriticModelPlaceholder"/>.</summary>
    public static readonly Dictionary<string, string> All = SkillResourceLoader.GetAllAgents();

    // skill-pair-merge: spectra-generation and spectra-execution agents merged into their flow skills.
    /// <summary>Spec 055: the context:fork critic subagent skill.</summary>
    public static string CriticAgent => All[CriticFile];

    /// <summary>Spec 097 (FR-022): an agent as installed — the critic with <paramref name="criticModel"/> in its frontmatter.</summary>
    public static string Render(string name, string content, string criticModel) =>
        name == CriticFile ? content.Replace(CriticModelPlaceholder, criticModel, StringComparison.Ordinal) : content;

    /// <summary>Spec 097 (FR-022): every bundled agent as <c>init</c> / <c>update-skills</c> install it.</summary>
    public static IEnumerable<(string Name, string Content)> Rendered(string criticModel) =>
        All.Select(a => (a.Key, Render(a.Key, a.Value, criticModel)));
}
