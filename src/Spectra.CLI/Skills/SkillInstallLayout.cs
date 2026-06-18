namespace Spectra.CLI.Skills;

/// <summary>
/// Spec 056/057 — install-target layout for the bundled orchestration artifacts under Claude Code.
/// Authoring skills install as <c>.claude/skills/&lt;name&gt;/SKILL.md</c>; the generation and execution
/// agents become main-session skills; the critic subagent lands in <c>.claude/agents/</c>. As of
/// Spec 057 (execution agent port) every bundled agent is routed under <c>.claude/</c>.
/// </summary>
public static class SkillInstallLayout
{
    /// <summary>Authoring skill (from <see cref="SkillContent"/>) → <c>.claude/skills/&lt;name&gt;/SKILL.md</c>.</summary>
    public static string SkillPath(string root, string skillName)
        => Path.Combine(root, ".claude", "skills", skillName, "SKILL.md");

    /// <summary>
    /// Agent file (from <see cref="AgentContent"/>) → role-specific target:
    /// the generation and execution agents become main-session skills; the critic a subagent.
    /// </summary>
    public static string AgentPath(string root, string agentFileName) => agentFileName switch
    {
        "spectra-critic.agent.md" => Path.Combine(root, ".claude", "agents", "spectra-critic.agent.md"),
        // skill-pair-merge: generation + execution agents merged into their flow skills; no agent install paths for them.
        // Fallback for any future not-yet-ported agent.
        _ => Path.Combine(root, ".github", "agents", agentFileName),
    };
}
