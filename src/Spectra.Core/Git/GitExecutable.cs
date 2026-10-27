namespace Spectra.Core.Git;

/// <summary>
/// Spec 087 (F2, FR-006/FR-007): finds the git program SPECTRA starts. Only absolute PATH entries are searched — never
/// the current folder, nor an empty, <c>.</c> or relative entry — and on Windows only <c>git.exe</c> (no
/// <c>.cmd</c> / <c>.bat</c>, no PATHEXT). Starting git by its bare name would let Windows run a <c>git.exe</c> from the
/// current folder (for example one committed to a repository being reviewed).
/// </summary>
public static class GitExecutable
{
    private static readonly Lazy<string?> Resolved = new(() => Resolve(
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(System.IO.Path.PathSeparator),
        Directory.GetCurrentDirectory(),
        OperatingSystem.IsWindows(),
        File.Exists));

    /// <summary>
    /// The absolute path of git, or null when no absolute PATH entry has it ("no git"). Resolved once per process, so
    /// the current-folder check uses the working folder at first use — the command's folder (research R1).
    /// </summary>
    public static string? Path => Resolved.Value;

    /// <summary>The first <c>git.exe</c> (Windows) / <c>git</c> in an absolute PATH entry that isn't the current folder.</summary>
    public static string? Resolve(
        IEnumerable<string?> pathEntries, string currentDirectory, bool isWindows, Func<string, bool> fileExists)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var current = Normalise(currentDirectory);
        var name = isWindows ? "git.exe" : "git";
        foreach (var raw in pathEntries)
        {
            var entry = (raw ?? string.Empty).Trim().Trim('"').Trim();
            if (entry.Length == 0 || entry == "." || !System.IO.Path.IsPathFullyQualified(entry))
            {
                continue;
            }

            var folder = Normalise(entry);
            if (folder is null || current is not null && string.Equals(folder, current, comparison))
            {
                continue;
            }

            var candidate = System.IO.Path.Combine(folder, name);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? Normalise(string path)
    {
        try
        {
            return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
