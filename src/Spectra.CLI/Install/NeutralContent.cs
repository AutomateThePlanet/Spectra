using System.Text.RegularExpressions;

namespace Spectra.CLI.Install;

/// <summary>
/// Spec 104: turns the bundled Claude Code content into the provider-neutral form installed for every other agent, at
/// emit time (the embedded source files are never edited). Skills share directories across agents (<c>.agents/skills</c>
/// is read by Codex, Gemini, Antigravity, Cursor and OpenCode), so the text is the same for all of them; each agent's
/// own vocabulary lives in its instructions fragment and its subagent frontmatter.
/// </summary>
public static class NeutralContent
{
    /// <summary>Ordered rewrites of Claude-only wording; the first match of a phrase wins.</summary>
    private static readonly (Regex Pattern, string Replacement)[] Wording =
    [
        (Rx(@"If you have no Agent tool, or your first Agent call fails"), "If you cannot start a subagent, or your first subagent call fails"),
        (Rx(@"subagent with the Agent tool"), "subagent"),
        (Rx(@"with the Agent tool"), "as a subagent"),
        (Rx(@"\bthe Agent tool\b"), "your subagent tool"),
        (Rx(@"\bAgent call\b"), "subagent call"),
        (Rx(@"Run with the Bash tool"), "Run in the shell"),
        (Rx(@"with the Bash tool"), "in the shell"),
        (Rx(@"\bthe Bash tool\b"), "the shell"),
        (Rx(@"with the Write tool"), "with your file-writing tool"),
        (Rx(@"\bthe Write tool\b"), "your file-writing tool"),
        (Rx(@"\bWrite tool\b"), "file-writing tool"),
        (Rx(@"with the Read tool"), "with your file-reading tool"),
        (Rx(@"\bthe Read tool\b"), "your file-reading tool"),
        (Rx(@"\bRead tool\b"), "file-reading tool"),
        (Rx(@"\(no Glob of `\.claude/skills/\*\*`,"), "(no listing of the skills folder,"),
        (Rx(@"do not Glob `\.claude/skills/\*\*`"), "do not list the skills folder"),
        (Rx(@"\(Grep/Glob\b"), "(search"),
        (Rx(@"\bUse Grep/Glob\b"), "Use search"),
        (Rx(@"\bGrep/Glob\b"), "search"),
        (Rx(@"`\.claude/skills/\*\*`"), "the skills folder"),
        (Rx(@"\bvia Claude Code\b"), "via your AI coding agent"),
        (Rx(@"\bthrough Claude Code\b"), "through your AI coding agent"),
        (Rx(@"\bfrom Claude Code\b"), "from your AI coding agent"),
        (Rx(@"\bin Claude Code\b"), "in your AI coding agent"),
        (Rx(@"\bClaude Code\b"), "your AI coding agent"),
    ];

    /// <summary>Text that must not survive the transform (a test keeps the bundled content free of them).</summary>
    public static readonly IReadOnlyList<string> ClaudeOnlyTokens =
    [
        "Agent tool", "Bash tool", "Write tool", "Read tool", "Edit tool", "Task tool", "Grep/Glob", ".claude/",
        "Claude Code", "context: fork", "disable-model-invocation",
        "{{GENERATE_TOOLS}}", "{{GENERATION_TOOLS}}", "{{READONLY_TOOLS}}", "{{EXECUTION_TOOLS}}", "{{CRITIC_MODEL}}",
    ];

    /// <summary>Neutral wording for any bundled text (line endings normalised to LF).</summary>
    public static string Text(string content)
    {
        var text = content.Replace("\r\n", "\n");
        foreach (var (pattern, replacement) in Wording)
        {
            text = pattern.Replace(text, replacement);
        }

        return text;
    }

    /// <summary>
    /// A bundled skill as installed for a non-Claude agent: Agent Skills frontmatter (<c>name</c> = folder name,
    /// <c>description</c>), the Claude <c>tools</c> key dropped, neutral body.
    /// </summary>
    public static string Skill(string skillName, string content)
    {
        var (frontmatter, body) = Split(content);
        var description = Field(frontmatter, "description")
            ?? throw new InvalidOperationException($"bundled skill '{skillName}' has no description");
        return $"---\nname: {skillName}\ndescription: {Yaml(Text(description))}\n---\n{Text(body)}";
    }

    /// <summary>A YAML double-quoted scalar (descriptions can hold <c>: </c>, which breaks a plain scalar).</summary>
    public static string Yaml(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>A bundled subagent, parsed into the parts every provider renders in its own format.</summary>
    public static AgentSpec Agent(string fileName, string content)
    {
        var (frontmatter, body) = Split(content);
        var name = Field(frontmatter, "name") ?? fileName.Replace(".agent.md", string.Empty, StringComparison.Ordinal);
        var description = Field(frontmatter, "description")
            ?? throw new InvalidOperationException($"bundled agent '{fileName}' has no description");
        var tools = (Field(frontmatter, "tools") ?? string.Empty)
            .Trim().TrimStart('[').TrimEnd(']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        return new AgentSpec(name, Text(description), tools, Text(body).TrimStart('\n'));
    }

    private static (string Frontmatter, string Body) Split(string content)
    {
        var text = content.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (string.Empty, text);
        }

        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        return end < 0 ? (string.Empty, text) : (text[4..end], text[(end + 5)..]);
    }

    private static string? Field(string frontmatter, string key)
    {
        foreach (var line in frontmatter.Split('\n'))
        {
            if (line.StartsWith(key + ":", StringComparison.Ordinal))
            {
                return line[(key.Length + 1)..].Trim();
            }
        }

        return null;
    }

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.CultureInvariant);
}

/// <summary>
/// Spec 104: a bundled subagent in neutral form. <paramref name="Tools"/> are the Claude tool names it declared
/// (<c>Read</c>, <c>Write</c>, <c>Bash</c>, <c>Agent</c>, …); each provider maps them to its own names.
/// </summary>
public sealed record AgentSpec(string Name, string Description, IReadOnlyList<string> Tools, string Body)
{
    /// <summary>The agent starts other subagents (the critic), so providers with an allow-list name it.</summary>
    public bool StartsSubagents => Tools.Contains("Agent");

    /// <summary>The agent writes files (every SPECTRA subagent does: the critic writes its verdict file).</summary>
    public bool Writes => Tools.Any(t => t is "Write" or "Edit");
}
