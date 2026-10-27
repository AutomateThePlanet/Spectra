namespace Spectra.CLI.Install;

/// <summary>Spec 104: a bad <c>--agent</c> value (unknown name, nothing detected, several detected). Exit code 2 (usage).</summary>
public sealed class AgentSelectionException(string message) : Exception(message);

/// <summary>
/// Spec 104: resolves <c>spectra init --agent</c> with BELLATRIX Agent's names, aliases and detection, so one value works
/// for both tools. <c>windsurf</c> → <c>devin</c> (with a notice: renamed Devin Desktop), <c>agy</c> → <c>antigravity</c>.
/// <c>auto</c> detects: the agents already installed in the repo, else exactly one agent whose markers exist; it never
/// guesses (none or several → an error listing the choices).
/// </summary>
public static class AgentSelection
{
    public const string Auto = "auto";

    /// <summary>Every supported agent, in the order results are reported.</summary>
    public static readonly IReadOnlyList<AgentTarget> All =
    [
        new ClaudeTarget(), new CopilotTarget(), new CursorTarget(), new DevinTarget(), new CodexTarget(),
        new GeminiTarget(), new AntigravityTarget(), new KiroTarget(), new OpenCodeTarget(),
    ];

    /// <summary>Accepted spellings that map to a canonical name without a notice.</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["agy"] = "antigravity",
    };

    public static IReadOnlyList<string> Names { get; } = All.Select(t => t.Name).ToArray();

    /// <summary>The agents other than Claude, for the one-line notice of a plain <c>spectra init</c>.</summary>
    public static string OthersList => string.Join(", ", Names.Where(n => n != "claude"));

    public static AgentTarget Get(string name) => All.Single(t => t.Name == name);

    /// <summary>Parses a comma-separated <c>--agent</c> value; returns canonical names in canonical order, de-duplicated.</summary>
    public static IReadOnlyList<string> Parse(string raw, string root, TextWriter notices)
    {
        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToArray();
        if (tokens.Length == 1 && tokens[0] == Auto)
        {
            return Detect(root);
        }

        var chosen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            var name = token;
            if (name == "windsurf")
            {
                notices.WriteLine("[spectra init] notice: 'windsurf' is now Devin Desktop — using --agent devin.");
                name = "devin";
            }
            else if (Aliases.TryGetValue(name, out var canonical))
            {
                name = canonical;
            }

            if (name == Auto)
            {
                throw new AgentSelectionException("--agent auto cannot be combined with agent names.");
            }

            if (!Names.Contains(name))
            {
                throw new AgentSelectionException($"Unknown --agent '{token}'. One of: {string.Join(", ", Names)} (or auto).");
            }

            chosen.Add(name);
        }

        if (chosen.Count == 0)
        {
            throw new AgentSelectionException($"--agent needs at least one of: {string.Join(", ", Names)} (or auto).");
        }

        return Names.Where(chosen.Contains).ToArray();
    }

    /// <summary>
    /// <c>--agent auto</c>: the agents already installed (a manifest in <c>.spectra/</c>, or Claude's skills), else the single
    /// agent whose repo markers exist.
    /// </summary>
    public static IReadOnlyList<string> Detect(string root)
    {
        var installed = Names.Where(n => IsInstalled(root, n)).ToArray();
        if (installed.Length > 0)
        {
            return installed;
        }

        var detected = All
            .Where(t => t.DetectionMarkers.Any(m =>
                File.Exists(Path.Combine(root, m)) || Directory.Exists(Path.Combine(root, m))))
            .Select(t => t.Name)
            .ToArray();

        return detected.Length switch
        {
            1 => detected,
            0 => throw new AgentSelectionException(
                $"No AI agent detected in '{root}'. Pass --agent <{string.Join("|", Names)}> (comma-separated for several)."),
            _ => throw new AgentSelectionException(
                $"Several AI agents detected ({string.Join(", ", detected)}). Pass --agent with the ones to install, e.g. --agent {string.Join(",", detected)}."),
        };
    }

    /// <summary>True when SPECTRA is installed for the agent: Claude's bundled skills, or the agent's manifest.</summary>
    public static bool IsInstalled(string root, string agent) =>
        agent == "claude"
            ? ClaudeInstalled(root)
            : File.Exists(AgentManifest.PathFor(root, agent));

    /// <summary>The unchanged Claude install is present (its skills reach agents that read <c>.claude/skills</c>).</summary>
    public static bool ClaudeInstalled(string root) =>
        File.Exists(Path.Combine(root, ".claude", "skills", "spectra-generate", "SKILL.md"));
}
