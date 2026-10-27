namespace Spectra.CLI.Skills;

/// <summary>
/// Spec 056/057 — install-target layout for the bundled orchestration artifacts under Claude Code.
/// Authoring skills install as <c>.claude/skills/&lt;name&gt;/SKILL.md</c>; the generation and execution
/// agents became main-session skills. Spec 092: every bundled agent (the critic and the update / criteria
/// subagents) is a Claude Code subagent under <c>.claude/agents/</c> — one rule, no per-name routing.
/// </summary>
public static class SkillInstallLayout
{
    /// <summary>Authoring skill (from <see cref="SkillContent"/>) → <c>.claude/skills/&lt;name&gt;/SKILL.md</c>.</summary>
    public static string SkillPath(string root, string skillName)
        => Path.Combine(root, ".claude", "skills", skillName, "SKILL.md");

    /// <summary>Spec 101 F2: a skill's reference file (from <see cref="SkillReferenceContent"/>) → <c>.claude/skills/&lt;name&gt;/reference.md</c>.</summary>
    public static string ReferencePath(string root, string skillName)
        => Path.Combine(root, ".claude", "skills", skillName, "reference.md");

    /// <summary>Agent file (from <see cref="AgentContent"/>) → <c>.claude/agents/&lt;file&gt;</c>, for every agent.</summary>
    public static string AgentPath(string root, string agentFileName)
        => Path.Combine(root, ".claude", "agents", agentFileName);
}
