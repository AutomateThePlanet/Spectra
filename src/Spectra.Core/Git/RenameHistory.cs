using System.Text;

namespace Spectra.Core.Git;

/// <summary>One rename from <c>git log --diff-filter=R</c>: owner-relative paths with <c>/</c>.</summary>
public sealed record GitRename(string Commit, long Time, string Old, string New, int Score);

/// <summary>
/// Spec 086 (D3, FR-005/FR-006, research R3): one owner's renames, newest first, and the candidates they give.
/// <list type="bullet">
/// <item>A path with two or more distinct rename targets anywhere in the history is ambiguous and never a candidate
/// (two branches, or a path re-created and renamed again).</item>
/// <item>Otherwise the chain is followed from the single target until a path that exists now.</item>
/// </list>
/// </summary>
public sealed class RenameHistory
{
    public const string Ambiguous = "ambiguous rename";
    public const string ChainEndsMissing = "rename chain ends at a missing file";
    public const string OutsideAllowedRoots = "renamed outside the allowed roots";
    private const int MaxChain = 100;

    private readonly Dictionary<string, SortedSet<string>> _targets = new(StringComparer.Ordinal);

    public RenameHistory(IReadOnlyList<GitRename> renames, int minSimilarity, bool shallow)
    {
        Renames = renames;
        MinSimilarity = minSimilarity;
        Shallow = shallow;
        foreach (var rename in renames)
        {
            if (!_targets.TryGetValue(rename.Old, out var set))
            {
                _targets[rename.Old] = set = new SortedSet<string>(StringComparer.Ordinal);
            }

            set.Add(rename.New);
        }
    }

    /// <summary>Newest first, as git logs them.</summary>
    public IReadOnlyList<GitRename> Renames { get; }

    public int MinSimilarity { get; }

    public bool Shallow { get; }

    /// <summary>The reason when the path has no rename at the threshold.</summary>
    public string NoRenameReason => NoRename(MinSimilarity, Shallow);

    public static string NoRename(int minSimilarity, bool shallow) =>
        $"no rename found at {minSimilarity}% similarity{(shallow ? " (shallow history)" : "")}";

    /// <summary>
    /// The path <paramref name="path"/> was renamed to, following the chain until <paramref name="exists"/> holds,
    /// or the reason there is none.
    /// </summary>
    public (string? Target, string? Reason) ForwardCandidate(string path, Func<string, bool> exists)
    {
        if (!_targets.ContainsKey(path))
        {
            return (null, NoRenameReason);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal) { path };
        var current = path;
        for (var step = 0; step < MaxChain; step++)
        {
            if (!_targets.TryGetValue(current, out var targets))
            {
                return (null, ChainEndsMissing);
            }

            if (targets.Count > 1)
            {
                return (null, Ambiguous);
            }

            var next = targets.Min!;
            if (exists(next))
            {
                return (next, null);
            }

            if (!visited.Add(next))
            {
                return (null, ChainEndsMissing);
            }

            current = next;
        }

        return (null, ChainEndsMissing);
    }

    /// <summary>
    /// The path a file had before <paramref name="renamesSincePoint"/> (newest first), given its path now: each
    /// rename whose new path is the current one is undone, newest to oldest.
    /// </summary>
    public static string PathAt(IEnumerable<GitRename> renamesSincePoint, string currentPath)
    {
        var path = currentPath;
        foreach (var rename in renamesSincePoint)
        {
            if (string.Equals(rename.New, path, StringComparison.Ordinal))
            {
                path = rename.Old;
            }
        }

        return path;
    }

    /// <summary>
    /// Parses <c>git log --diff-filter=R --name-status -z --format=%x1e%H[%x00%ct]</c>: records start with 0x1E, fields
    /// are NUL-separated, and each rename is <c>R&lt;score&gt;</c>, old, new. Paths are raw UTF-8.
    /// </summary>
    public static IReadOnlyList<GitRename> Parse(byte[] stdout)
    {
        var renames = new List<GitRename>();
        foreach (var record in Encoding.UTF8.GetString(stdout).Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Split('\0');
            var commit = fields[0].Trim();
            long time = 0;
            for (var i = 1; i < fields.Length; i++)
            {
                var field = fields[i].TrimStart('\n', '\r');
                if (i == 1 && field.Length > 0 && field.All(char.IsAsciiDigit))
                {
                    time = long.Parse(field, System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }

                if (field.Length > 1 && field[0] == 'R' && field.Skip(1).All(char.IsAsciiDigit) && i + 2 < fields.Length)
                {
                    var score = int.Parse(field.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
                    renames.Add(new GitRename(commit, time, fields[i + 1], fields[i + 2], score));
                    i += 2;
                }
            }
        }

        return renames;
    }
}
