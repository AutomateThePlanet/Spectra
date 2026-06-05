namespace Spectra.CLI.Skills;

/// <summary>
/// Spec 056 — install-target layout for the bundled orchestration artifacts under Claude Code.
/// Authoring skills install as <c>.claude/skills/&lt;name&gt;/SKILL.md</c>; the generation agent is
/// ported into a main-session skill; the critic subagent lands in <c>.claude/agents/</c>. The
/// execution agent is deliberately left on its existing <c>.github/agents/</c> install — it is ported
/// by the next spec, so this layout keeps that scope boundary explicit.
/// </summary>
public static class SkillInstallLayout
{
    /// <summary>Authoring skill (from <see cref="SkillContent"/>) → <c>.claude/skills/&lt;name&gt;/SKILL.md</c>.</summary>
    public static string SkillPath(string root, string skillName)
        => Path.Combine(root, ".claude", "skills", skillName, "SKILL.md");

    /// <summary>
    /// Agent file (from <see cref="AgentContent"/>) → role-specific target:
    /// the generation agent becomes a main-session skill, the critic a subagent, and the execution
    /// agent stays on <c>.github/agents/</c> (excluded from this port).
    /// </summary>
    public static string AgentPath(string root, string agentFileName) => agentFileName switch
    {
        "spectra-generation.agent.md" => Path.Combine(root, ".claude", "skills", "spectra-generation", "SKILL.md"),
        "spectra-critic.agent.md" => Path.Combine(root, ".claude", "agents", "spectra-critic.agent.md"),
        // Execution agent (and any future not-yet-ported agent): unchanged .github/ install.
        _ => Path.Combine(root, ".github", "agents", agentFileName),
    };
}
