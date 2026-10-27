using System.Text.RegularExpressions;

namespace Spectra.CLI.Skills;

/// <summary>
/// Spec 096 (F1; research R1, analyze M1): the one key form of the skills manifest — the file's repo-relative path with
/// <c>/</c> separators. Older manifests mixed forms: <c>spectra init</c> wrote relative keys (with <c>\</c> on Windows)
/// for templates, the profile, CUSTOMIZATION.md and USAGE.md, and absolute keys for skills and agents, which
/// <c>update-skills</c> also used. <see cref="Normalize"/> maps any of them to the one form; an absolute key from another
/// location (a moved workspace) is re-rooted by its install-path tail.
/// </summary>
public static partial class ManifestKey
{
    /// <summary>The manifest key of <paramref name="path"/> (absolute, or relative to <paramref name="root"/>).</summary>
    public static string For(string root, string path) =>
        Normalize(root, path) ?? Clean(path);

    /// <summary>
    /// The normalised key, or null for an absolute key outside <paramref name="root"/> that matches no install path.
    /// </summary>
    public static string? Normalize(string root, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (!IsAbsolute(key))
        {
            return Clean(key);
        }

        if (Path.IsPathRooted(key))
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(key));
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relative.StartsWith("../", StringComparison.Ordinal))
            {
                return Clean(relative);
            }
        }

        return InstallTail(key);
    }

    /// <summary>
    /// Spec 098 (F4): how new a pre-096 record under <paramref name="key"/> is — 3 an absolute key under
    /// <paramref name="root"/> (written by update-skills, which runs after init), 2 an absolute key from another location
    /// (a moved workspace), 1 a relative key (init).
    /// </summary>
    public static int Rank(string root, string key)
    {
        if (!IsAbsolute(key))
        {
            return 1;
        }

        if (Path.IsPathRooted(key))
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(key));
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relative.StartsWith("../", StringComparison.Ordinal))
            {
                return 3;
            }
        }

        return 2;
    }

    /// <summary>
    /// Analyze M1: the install path a foreign absolute key ends with — <c>.claude/skills/&lt;name&gt;/SKILL.md</c>,
    /// <c>.claude/skills/&lt;name&gt;/reference.md</c> (Spec 101), <c>.claude/agents/&lt;file&gt;</c>, <c>.spectra/prompts/&lt;id&gt;.md</c>, <c>profiles/_default.yaml</c>,
    /// <c>CUSTOMIZATION.md</c> or <c>USAGE.md</c> — matched on the last segments (case-insensitive on Windows), or null.
    /// </summary>
    public static string? InstallTail(string key)
    {
        var segments = key.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        bool At(int fromEnd, string value) =>
            segments.Length >= fromEnd && string.Equals(segments[^fromEnd], value, comparison);

        if (segments.Length >= 4 && At(4, ".claude") && At(3, "skills") && At(1, "SKILL.md"))
        {
            return $".claude/skills/{segments[^2]}/SKILL.md";
        }

        if (segments.Length >= 4 && At(4, ".claude") && At(3, "skills") && At(1, "reference.md"))
        {
            return $".claude/skills/{segments[^2]}/reference.md";
        }

        if (segments.Length >= 3 && At(3, ".claude") && At(2, "agents"))
        {
            return $".claude/agents/{segments[^1]}";
        }

        if (segments.Length >= 3 && At(3, ".spectra") && At(2, "prompts") && segments[^1].EndsWith(".md", comparison))
        {
            return $".spectra/prompts/{segments[^1]}";
        }

        if (segments.Length >= 2 && At(2, "profiles") && At(1, "_default.yaml"))
        {
            return "profiles/_default.yaml";
        }

        if (At(1, "CUSTOMIZATION.md"))
        {
            return "CUSTOMIZATION.md";
        }

        return At(1, "USAGE.md") ? "USAGE.md" : null;
    }

    /// <summary>Absolute on any platform: rooted here, or a Windows drive path / UNC path written on another machine.</summary>
    private static bool IsAbsolute(string key) =>
        Path.IsPathRooted(key) || DrivePath().IsMatch(key) || key.StartsWith(@"\\", StringComparison.Ordinal);

    private static string Clean(string path)
    {
        var clean = path.Replace('\\', '/');
        while (clean.StartsWith("./", StringComparison.Ordinal))
        {
            clean = clean[2..];
        }

        return clean;
    }

    [GeneratedRegex(@"^[A-Za-z]:[\\/]")]
    private static partial Regex DrivePath();
}
