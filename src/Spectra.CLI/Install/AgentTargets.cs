using System.Text;

namespace Spectra.CLI.Install;

/// <summary>Spec 104 (R1/R3): one marker block that registers SPECTRA in a human-owned instructions file.</summary>
/// <param name="Path">Repo-relative path of the host file (<c>AGENTS.md</c>, <c>GEMINI.md</c>).</param>
/// <param name="Line">The single line inside the block (a pointer sentence or an <c>@</c> import).</param>
public sealed record HostRegistration(string Path, string Line);

/// <summary>
/// Spec 104: placement and frontmatter for one AI agent. Content is the provider-neutral form (<see cref="NeutralContent"/>);
/// a target only decides <b>where</b> each piece lands and <b>which frontmatter</b> the agent's loader expects. The layout
/// follows BELLATRIX Agent's provider emitters (same names, aliases and detection markers, so the two tools coexist);
/// every path and field is checked against the provider's docs (see <c>FINDINGS-multi-client-gap-v2.md</c> §4).
/// </summary>
public abstract class AgentTarget
{
    /// <summary>The shared fragment, for agents that register through a host file instead of a native rule file.</summary>
    public const string SharedFragmentPath = ".spectra/spectra.instructions.md";

    /// <summary>Claude Code's skills directory (written by the unchanged Claude install, never by this layer).</summary>
    public const string ClaudeSkillsDir = ".claude/skills";

    /// <summary>The <c>--agent</c> value.</summary>
    public abstract string Name { get; }

    /// <summary>Product name for messages.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Skill directories the agent loads from the repo, its native one first (input to the set-cover planner).</summary>
    public abstract IReadOnlyList<string> SkillDirs { get; }

    /// <summary>True when the agent loads <c>.claude/agents</c>, so Claude's subagents reach it without a copy.</summary>
    public virtual bool ReadsClaudeAgents => false;

    /// <summary>True when the agent's own subagent wins a name clash with <c>.claude/agents</c> (a native copy is safe).</summary>
    public virtual bool NativeAgentsShadowClaude => false;

    /// <summary>Repo-relative path of the agent's always-on copy of the instructions fragment.</summary>
    public abstract string FragmentPath { get; }

    /// <summary>The fragment as the agent loads it (its frontmatter, if any, + the shared body).</summary>
    public virtual string RenderFragment(string body) => body;

    /// <summary>The marker block written into the agent's human-owned instructions file, if it needs one.</summary>
    public virtual HostRegistration? Host => null;

    /// <summary>Repo-relative path of a subagent file.</summary>
    public abstract string AgentPath(string agentName);

    /// <summary>A subagent in the agent's native format.</summary>
    public abstract string RenderAgent(AgentSpec agent);

    /// <summary>The project-level command allowlist, where the agent documents one.</summary>
    public virtual AllowlistTarget? Allowlist => null;

    /// <summary>Repo-root markers that show the agent is in use (<c>--agent auto</c>).</summary>
    public abstract IReadOnlyList<string> DetectionMarkers { get; }

    /// <summary>The <c>AGENTS.md</c> block shared by every agent that reads it (Codex, OpenCode): one block, written once.</summary>
    protected static HostRegistration AgentsMdPointer { get; } = new("AGENTS.md",
        $"Before any SPECTRA work (test generation, test updates, acceptance criteria, test execution), read `{SharedFragmentPath}` and follow it.");

    protected static string Yaml(string value) => NeutralContent.Yaml(value);

    protected static string MarkdownAgent(AgentSpec agent, string extraFrontmatter) =>
        $"---\nname: {agent.Name}\ndescription: {Yaml(agent.Description)}\n{extraFrontmatter}---\n\n{agent.Body}";

    /// <summary>Maps the Claude tool names an agent declared, keeping order and dropping duplicates and unmapped ones.</summary>
    protected static List<string> Map(AgentSpec agent, Func<string, string?> map)
    {
        var mapped = new List<string>();
        foreach (var name in agent.Tools.Select(map).OfType<string>())
        {
            if (!mapped.Contains(name))
            {
                mapped.Add(name);
            }
        }

        return mapped;
    }
}

/// <summary>
/// Claude Code. Installed by the unchanged Claude path (<c>InitHandler</c>); listed here only so <c>--agent</c> parsing,
/// detection and the skill-directory planner see it.
/// </summary>
public sealed class ClaudeTarget : AgentTarget
{
    public override string Name => "claude";
    public override string DisplayName => "Claude Code";
    public override IReadOnlyList<string> SkillDirs { get; } = [ClaudeSkillsDir];
    public override bool ReadsClaudeAgents => true;
    public override string FragmentPath => throw new NotSupportedException("the Claude install is not emitted by this layer");
    public override string AgentPath(string agentName) => $".claude/agents/{agentName}.agent.md";
    public override string RenderAgent(AgentSpec agent) => throw new NotSupportedException("the Claude install is not emitted by this layer");
    public override IReadOnlyList<string> DetectionMarkers { get; } = [".claude", "CLAUDE.md"];
}

/// <summary>
/// GitHub Copilot (VS Code, CLI, cloud agent): an always-applied <c>.github/instructions/spectra.instructions.md</c>;
/// <c>.github/skills</c> (it also reads <c>.claude/skills</c> and <c>.agents/skills</c>); <c>.github/agents/*.agent.md</c>
/// with Copilot tool aliases and an <c>agents:</c> allow-list naming the critic on agents that start it.
/// </summary>
public sealed class CopilotTarget : AgentTarget
{
    public override string Name => "copilot";
    public override string DisplayName => "GitHub Copilot";
    public override IReadOnlyList<string> SkillDirs { get; } = [".github/skills", ClaudeSkillsDir, ".agents/skills"];
    public override bool ReadsClaudeAgents => true;
    public override string FragmentPath => ".github/instructions/spectra.instructions.md";
    public override string RenderFragment(string body) => "---\napplyTo: \"**\"\n---\n\n" + body;
    public override string AgentPath(string agentName) => $".github/agents/{agentName}.agent.md";

    public override string RenderAgent(AgentSpec agent)
    {
        var tools = Map(agent, tool => tool switch
        {
            "Bash" => "execute",
            "Read" => "read",
            "Edit" or "Write" => "edit",
            "Grep" or "Glob" => "search",
            "Agent" => "agent",
            _ => null,
        });
        var extra = new StringBuilder();
        if (tools.Count > 0)
        {
            extra.Append("tools: [").Append(string.Join(", ", tools.Select(t => $"'{t}'"))).Append("]\n");
        }

        if (agent.StartsSubagents)
        {
            extra.Append("agents: ['spectra-critic']\n");
        }

        return MarkdownAgent(agent, extra.ToString());
    }

    public override IReadOnlyList<string> DetectionMarkers { get; } = [".github/copilot-instructions.md", ".github/agents"];
}

/// <summary>
/// Cursor: an <c>alwaysApply</c> rule <c>.cursor/rules/spectra.mdc</c>; <c>.cursor/skills</c>; <c>.cursor/agents</c> (wins a
/// clash with <c>.claude/agents</c>). Cursor has no tool list, only <c>readonly</c>, which blocks file edits — every SPECTRA
/// subagent writes (the critic its verdict file), so none is marked read-only.
/// </summary>
public sealed class CursorTarget : AgentTarget
{
    public override string Name => "cursor";
    public override string DisplayName => "Cursor";
    public override IReadOnlyList<string> SkillDirs { get; } = [".cursor/skills", ".agents/skills", ClaudeSkillsDir, ".codex/skills"];
    public override bool ReadsClaudeAgents => true;
    public override bool NativeAgentsShadowClaude => true;
    public override string FragmentPath => ".cursor/rules/spectra.mdc";

    public override string RenderFragment(string body) =>
        "---\ndescription: SPECTRA test generation and execution through the spectra CLI (always applied).\nalwaysApply: true\n---\n\n" + body;

    public override string AgentPath(string agentName) => $".cursor/agents/{agentName}.md";
    public override string RenderAgent(AgentSpec agent) => MarkdownAgent(agent, agent.Writes ? string.Empty : "readonly: true\n");
    public override AllowlistTarget Allowlist { get; } = AllowlistTarget.StringArray(".cursor/cli.json", ["permissions", "allow"], "Shell(spectra)");
    public override IReadOnlyList<string> DetectionMarkers { get; } = [".cursor"];
}

/// <summary>
/// Devin Desktop (formerly Windsurf) / Devin CLI: an <c>always_on</c> rule <c>.devin/rules/spectra.md</c>; <c>.devin/skills</c>
/// (its documented skill import is <c>.github/skills</c>); <c>.devin/agents</c>. <c>allowed-tools</c> is written only when
/// every tool has a documented Devin name (<c>read</c>, <c>grep</c>, <c>glob</c>, <c>exec</c>); there is none for writing
/// files, so SPECTRA's subagents stay unrestricted.
/// </summary>
public sealed class DevinTarget : AgentTarget
{
    public override string Name => "devin";
    public override string DisplayName => "Devin";
    public override IReadOnlyList<string> SkillDirs { get; } = [".devin/skills", ".github/skills"];
    public override string FragmentPath => ".devin/rules/spectra.md";
    public override string RenderFragment(string body) => "---\ntrigger: always_on\n---\n\n" + body;
    public override string AgentPath(string agentName) => $".devin/agents/{agentName}.md";

    public override string RenderAgent(AgentSpec agent)
    {
        string? Documented(string tool) => tool switch { "Read" => "read", "Grep" => "grep", "Glob" => "glob", "Bash" => "exec", _ => null };
        var allDocumented = agent.Tools.All(t => Documented(t) is not null);
        var tools = Map(agent, Documented);
        return MarkdownAgent(agent, allDocumented && tools.Count > 0 ? $"allowed-tools: [{string.Join(", ", tools)}]\n" : string.Empty);
    }

    public override AllowlistTarget Allowlist { get; } = AllowlistTarget.StringArray(".devin/config.json", ["permissions", "allow"], "Exec(spectra)");
    public override IReadOnlyList<string> DetectionMarkers { get; } = [".devin", ".windsurf"];
}

/// <summary>
/// OpenAI Codex CLI: no tool-owned auto-loaded file, so <c>AGENTS.md</c> gets one marker block pointing at the shared
/// fragment; <c>.agents/skills</c>; TOML subagents in <c>.codex/agents</c> (<c>developer_instructions</c> holds the body).
/// </summary>
public sealed class CodexTarget : AgentTarget
{
    public override string Name => "codex";
    public override string DisplayName => "Codex CLI";
    public override IReadOnlyList<string> SkillDirs { get; } = [".agents/skills"];
    public override string FragmentPath => SharedFragmentPath;
    public override HostRegistration Host => AgentsMdPointer;
    public override string AgentPath(string agentName) => $".codex/agents/{agentName}.toml";

    public override string RenderAgent(AgentSpec agent) =>
        $"name = {TomlString(agent.Name)}\ndescription = {TomlString(agent.Description)}\ndeveloper_instructions = {TomlMultiline(agent.Body)}\n";

    private static string TomlString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>A literal multi-line string (no escapes) unless the body contains its delimiter.</summary>
    private static string TomlMultiline(string body) =>
        body.Contains("'''", StringComparison.Ordinal)
            ? "\"\"\"\n" + body.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"\"\"", "\"\"\\\"", StringComparison.Ordinal) + "\"\"\""
            : "'''\n" + body + "'''";

    public override IReadOnlyList<string> DetectionMarkers { get; } = [".codex"];
}

/// <summary>
/// Gemini CLI (enterprise / Gemini Code Assist): one marker block in <c>GEMINI.md</c> importing the shared fragment;
/// <c>.agents/skills</c>; <c>.gemini/agents</c> (<c>kind: local</c>) with Gemini built-in tool names.
/// </summary>
public sealed class GeminiTarget : AgentTarget
{
    public override string Name => "gemini";
    public override string DisplayName => "Gemini CLI";
    public override IReadOnlyList<string> SkillDirs { get; } = [".agents/skills", ".gemini/skills"];
    public override string FragmentPath => SharedFragmentPath;
    public override HostRegistration Host { get; } = new("GEMINI.md", "@./" + SharedFragmentPath);
    public override string AgentPath(string agentName) => $".gemini/agents/{agentName}.md";

    public override string RenderAgent(AgentSpec agent)
    {
        var tools = Map(agent, tool => tool switch
        {
            "Read" => "read_file",
            "Grep" => "grep_search",
            "Glob" => "glob",
            "Bash" => "run_shell_command",
            "Write" => "write_file",
            "Edit" => "replace",
            _ => null,
        });
        var block = tools.Count > 0 ? "tools:\n" + string.Concat(tools.Select(t => $"  - {t}\n")) : string.Empty;
        return MarkdownAgent(agent, "kind: local\n" + block);
    }

    public override IReadOnlyList<string> DetectionMarkers { get; } = [".gemini", "GEMINI.md"];
}

/// <summary>
/// Google Antigravity CLI (<c>agy</c>): an <c>always_on</c> workspace rule <c>.agents/rules/spectra.md</c> (a rule without
/// frontmatter is discarded); <c>.agents/skills</c>; subagents in the folder form <c>.agents/agents/&lt;name&gt;/agent.md</c>.
/// <c>tools</c> defaults to none and an unknown name can hang a subagent, so only documented names are listed.
/// </summary>
public sealed class AntigravityTarget : AgentTarget
{
    public override string Name => "antigravity";
    public override string DisplayName => "Antigravity CLI";
    public override IReadOnlyList<string> SkillDirs { get; } = [".agents/skills"];
    public override string FragmentPath => ".agents/rules/spectra.md";
    public override string RenderFragment(string body) => "---\ntrigger: always_on\n---\n\n" + body;
    public override string AgentPath(string agentName) => $".agents/agents/{agentName}/agent.md";

    public override string RenderAgent(AgentSpec agent)
    {
        string[] tools = agent.Writes
            ? ["view_file", "grep_search", "replace_file_content", "run_command"]
            : ["view_file", "grep_search", "run_command"];
        return MarkdownAgent(agent, "tools:\n" + string.Concat(tools.Select(t => $"  - {t}\n")) + "subagent: true\nmainAgent: false\n");
    }

    public override IReadOnlyList<string> DetectionMarkers { get; } = [".agents/agents", ".agents/rules", ".agents/mcp_config.json", ".agent"];
}

/// <summary>
/// Kiro CLI: an always-included steering file <c>.kiro/steering/spectra.md</c>; <c>.kiro/skills</c>; Markdown agents in
/// <c>.kiro/agents</c>. Custom agents load neither steering nor skills unless <c>resources</c> lists them, so every agent
/// lists SPECTRA's steering file and skills. <c>tools</c> uses Kiro's category tags.
/// </summary>
public sealed class KiroTarget : AgentTarget
{
    public override string Name => "kiro";
    public override string DisplayName => "Kiro CLI";
    public override IReadOnlyList<string> SkillDirs { get; } = [".kiro/skills"];
    public override string FragmentPath => ".kiro/steering/spectra.md";
    public override string RenderFragment(string body) => "---\ninclusion: always\n---\n\n" + body;
    public override string AgentPath(string agentName) => $".kiro/agents/{agentName}.md";

    public override string RenderAgent(AgentSpec agent)
    {
        var tags = new List<string> { "read" };
        if (agent.Writes)
        {
            tags.Add("write");
        }

        tags.Add("shell");
        if (agent.StartsSubagents)
        {
            tags.Add("subagent");
        }

        return MarkdownAgent(agent,
            $"tools: [{string.Join(", ", tags.Select(t => $"\"{t}\""))}]\nresources:\n"
            + $"  - file://{FragmentPath}\n  - skill://.kiro/skills/spectra-*/SKILL.md\n");
    }

    public override IReadOnlyList<string> DetectionMarkers { get; } = [".kiro"];
}

/// <summary>
/// OpenCode: reads <c>AGENTS.md</c> (no <c>@</c> imports), so it shares Codex's pointer block; <c>.opencode/skills</c> (it
/// also reads <c>.agents/skills</c> and <c>.claude/skills</c>); Markdown agents in <c>.opencode/agents</c> (file name = name).
/// </summary>
public sealed class OpenCodeTarget : AgentTarget
{
    public override string Name => "opencode";
    public override string DisplayName => "OpenCode";
    public override IReadOnlyList<string> SkillDirs { get; } = [".opencode/skills", ".agents/skills", ClaudeSkillsDir];
    public override string FragmentPath => SharedFragmentPath;
    public override HostRegistration Host => AgentsMdPointer;
    public override string AgentPath(string agentName) => $".opencode/agents/{agentName}.md";

    public override string RenderAgent(AgentSpec agent) =>
        agent.Writes
            ? $"---\ndescription: {Yaml(agent.Description)}\nmode: subagent\n---\n\n{agent.Body}"
            : $"---\ndescription: {Yaml(agent.Description)}\nmode: subagent\npermission:\n  edit: deny\n---\n\n{agent.Body}";

    public override AllowlistTarget Allowlist { get; } = AllowlistTarget.PatternMap("opencode.json", ["permission", "bash"], "spectra *", "allow");
    public override IReadOnlyList<string> DetectionMarkers { get; } = ["opencode.json", "opencode.jsonc", ".opencode"];
}
