namespace Spectra.Core.Git;

/// <summary>
/// Spec 087 (F3, FR-009–FR-011): the environment of every git process SPECTRA starts — the git reader's runner and the
/// execution identity lookup. Variables that point git at another repository, work tree, index, object store or
/// configuration (as a git hook or a shell may have set) are removed, so a call reads the repository it names; optional
/// locks and terminal prompts stay off.
/// </summary>
public static class GitEnvironment
{
    public static IReadOnlyList<string> RedirectingVariables { get; } =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_CEILING_DIRECTORIES", "GIT_DISCOVERY_ACROSS_FILESYSTEM", "GIT_CONFIG",
        "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT"
    ];

    private static readonly string[] RedirectingPrefixes = ["GIT_CONFIG_KEY_", "GIT_CONFIG_VALUE_"];

    /// <summary>Set on every call: git takes no optional locks (no <c>index.lock</c>) and never prompts.</summary>
    public static IReadOnlyDictionary<string, string> Settings { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["GIT_TERMINAL_PROMPT"] = "0"
    };

    /// <summary>Whether <paramref name="name"/> redirects git; names compare ignoring case on Windows by default.</summary>
    public static bool IsRedirecting(string name) => IsRedirecting(name, OperatingSystem.IsWindows());

    public static bool IsRedirecting(string name, bool ignoreCase)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return RedirectingVariables.Any(v => string.Equals(v, name, comparison))
            || RedirectingPrefixes.Any(p => name.StartsWith(p, comparison));
    }

    /// <summary>
    /// Removes every redirecting variable from <paramref name="environment"/> (a process start's inherited copy), then
    /// sets <see cref="Settings"/>, then <paramref name="extra"/>.
    /// </summary>
    public static void Apply(IDictionary<string, string?> environment, IReadOnlyDictionary<string, string>? extra = null)
    {
        foreach (var name in environment.Keys.Where(IsRedirecting).ToList())
        {
            environment.Remove(name);
        }

        foreach (var (name, value) in Settings)
        {
            environment[name] = value;
        }

        foreach (var (name, value) in extra ?? new Dictionary<string, string>())
        {
            environment[name] = value;
        }
    }
}
