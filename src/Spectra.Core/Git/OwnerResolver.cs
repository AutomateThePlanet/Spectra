namespace Spectra.Core.Git;

/// <summary>A repository that owns docs: its root (full path), whether it is a submodule, whether it is shallow, HEAD.</summary>
public sealed record GitOwner(string Root, bool IsSubmodule, bool Shallow, string Head);

/// <summary>
/// Spec 086 (D2, FR-004, research R2/R3): finds the nearest repository containing a path (from its folder, or the
/// nearest existing parent folder when the folder is gone), and loads each owner's rename history once per threshold.
/// Submodules and nested submodules are ordinary owners. Everything is cached per instance (one command).
/// </summary>
public sealed class OwnerResolver
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly GitReader _reader;
    private readonly Dictionary<string, (GitOwner? Owner, string? Reason)> _byDirectory = new(PathComparer);
    private readonly Dictionary<string, GitOwner> _byRoot = new(PathComparer);
    private readonly Dictionary<(string Root, int Threshold), (RenameHistory? History, string? Reason)> _histories = new();

    public OwnerResolver(GitReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public GitReader Reader => _reader;

    /// <summary>The owner of <paramref name="fullPath"/> (a file that may no longer exist), or the reason there is none.</summary>
    public Task<(GitOwner? Owner, string? Reason)> OwnerOfAsync(string fullPath, CancellationToken ct) =>
        OwnerOfDirectoryAsync(Path.GetDirectoryName(Path.GetFullPath(fullPath)), ct);

    /// <summary>The repository containing <paramref name="directory"/> (or its nearest existing parent).</summary>
    public async Task<(GitOwner? Owner, string? Reason)> OwnerOfDirectoryAsync(string? directory, CancellationToken ct)
    {
        directory = NearestExistingDirectory(directory is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)));
        if (directory is null)
        {
            return (null, GitReader.NotARepository);
        }

        if (_byDirectory.TryGetValue(directory, out var cached))
        {
            return cached;
        }

        var result = await _reader.RunAsync(directory, "rev-parse",
            ["--show-toplevel", "--show-superproject-working-tree", "--is-shallow-repository", "HEAD"], null, ct);
        (GitOwner? Owner, string? Reason) outcome = result.Ok ? (Parse(result.Text), null) : (null, result.Unavailable);
        if (outcome.Owner is null && outcome.Reason is null)
        {
            outcome = (null, "git failed: unexpected rev-parse output");
        }

        _byDirectory[directory] = outcome;
        return outcome;
    }

    /// <summary>A full path as git names it inside <paramref name="owner"/>: relative, with <c>/</c>.</summary>
    public static string RelativePath(GitOwner owner, string fullPath) =>
        Path.GetRelativePath(owner.Root, Path.GetFullPath(fullPath)).Replace('\\', '/');

    /// <summary>The owner's renames at <paramref name="minSimilarity"/>%: one <c>git log</c> per owner and threshold.</summary>
    public async Task<(RenameHistory? History, string? Reason)> RenameHistoryAsync(
        GitOwner owner, int minSimilarity, CancellationToken ct)
    {
        var key = (owner.Root, minSimilarity);
        if (_histories.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = await _reader.RunAsync(owner.Root, "log",
            ["-l0", $"-M{minSimilarity}%", "--diff-filter=R", "--name-status", "-z", "--format=%x1e%H%x00%ct", "HEAD"], null, ct);
        (RenameHistory? History, string? Reason) outcome = result.Ok
            ? (new RenameHistory(RenameHistory.Parse(result.Stdout), minSimilarity, owner.Shallow), null)
            : (null, result.Unavailable);
        _histories[key] = outcome;
        return outcome;
    }

    private GitOwner? Parse(string text)
    {
        // Defensive (research R2): the head is 40 (or 64) hex characters, the shallow flag true/false, the first path
        // the top level, a second path the superproject (absent when empty).
        string? top = null, super = null, head = null;
        var shallow = false;
        foreach (var line in text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0))
        {
            if (line is "true" or "false")
            {
                shallow = line == "true";
            }
            else if (line.Length is 40 or 64 && line.All(Uri.IsHexDigit))
            {
                head = line;
            }
            else if (top is null)
            {
                top = line;
            }
            else
            {
                super ??= line;
            }
        }

        if (top is null || head is null)
        {
            return null;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(top));
        if (!_byRoot.TryGetValue(root, out var owner))
        {
            _byRoot[root] = owner = new GitOwner(root, super is not null, shallow, head);
        }

        return owner;
    }

    private static string? NearestExistingDirectory(string? directory)
    {
        while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return string.IsNullOrEmpty(directory) ? null : directory;
    }
}
