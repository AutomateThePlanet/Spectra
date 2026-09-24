namespace Spectra.Core.Coverage;

/// <summary>
/// Resolves a test's <c>source_refs</c> to documentation files (Spec 078). Model-free and deterministic;
/// never throws. The only I/O is <see cref="File.Exists(string)"/>.
/// </summary>
/// <remarks>
/// Per ref: strip the <c>#fragment</c> and normalise separators; then try, in order, a fully-qualified path
/// under the repo root, the ref relative to the repo root, and the ref relative to the configured source
/// directory (legacy bare refs such as <c>file.md</c>). Candidates outside the repo root never resolve.
/// </remarks>
public sealed class SourceRefResolver
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private readonly string _repoRoot;
    private readonly string _sourceDir;

    public SourceRefResolver(string repoRoot, string sourceDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDir);

        _repoRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoRoot));
        _sourceDir = Path.GetFullPath(Path.IsPathRooted(sourceDir) ? sourceDir : Path.Combine(_repoRoot, sourceDir));
    }

    public SourceRefResolution Resolve(IEnumerable<string> refs)
    {
        ArgumentNullException.ThrowIfNull(refs);

        var resolved = new List<ResolvedSourceRef>();
        var missing = new List<MissingSourceRef>();

        foreach (var sourceRef in refs)
        {
            var outcome = ResolveOne(sourceRef ?? string.Empty);
            if (outcome.Resolved is not null)
            {
                resolved.Add(outcome.Resolved);
            }
            else
            {
                missing.Add(new MissingSourceRef(sourceRef ?? string.Empty, outcome.Reason));
            }
        }

        var comparer = PathComparison == StringComparison.Ordinal
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
        var distinct = resolved.Select(r => r.FullPath).Distinct(comparer).ToList();

        return new SourceRefResolution(resolved, missing, distinct);
    }

    private (ResolvedSourceRef? Resolved, MissingReason Reason) ResolveOne(string sourceRef)
    {
        var path = SourceRefNormalizer.StripFragment(sourceRef).Trim().Replace('\\', '/');
        if (path.Length == 0)
        {
            return (null, MissingReason.Empty);
        }

        // 1. Fully-qualified path (e.g. C:/repo/docs/a.md, or /abs on Unix) that lies under the repo root.
        if (Path.IsPathFullyQualified(path) && TryFile(path) is { } absolute)
        {
            return (Resolved(sourceRef, absolute), MissingReason.NotFound);
        }

        var relative = path.TrimStart('/');
        if (relative.Length == 0)
        {
            return (null, MissingReason.Empty);
        }

        // A repo-relative ref that climbs out of the repository never resolves, even via the fallback.
        var repoCandidate = FullPathOrNull(Path.Combine(_repoRoot, relative));
        if (repoCandidate is null || !IsUnderRoot(repoCandidate))
        {
            return (null, MissingReason.OutsideRepository);
        }

        // 2. Relative to the repo root (the default generation profile writes docs/<file>.md#<Section>).
        // 3. Legacy fallback: relative to the configured source directory (bare refs such as file.md).
        var found = TryFile(repoCandidate) ?? TryFile(Path.Combine(_sourceDir, relative));
        return found is null
            ? (null, MissingReason.NotFound)
            : (Resolved(sourceRef, found), MissingReason.NotFound);
    }

    private ResolvedSourceRef Resolved(string sourceRef, string fullPath) =>
        new(sourceRef, Path.GetRelativePath(_repoRoot, fullPath).Replace('\\', '/'), fullPath);

    /// <summary>The candidate's full path when it is an existing file under the repo root; otherwise null.</summary>
    private string? TryFile(string candidate)
    {
        var full = FullPathOrNull(candidate);
        return full is not null && IsUnderRoot(full) && File.Exists(full) ? full : null;
    }

    private static string? FullPathOrNull(string candidate)
    {
        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private bool IsUnderRoot(string fullPath) =>
        fullPath.StartsWith(_repoRoot + Path.DirectorySeparatorChar, PathComparison);
}

/// <summary>Result of resolving a set of source refs.</summary>
/// <param name="Resolved">Refs that resolved to an existing file, in input order.</param>
/// <param name="Missing">Refs that did not resolve, in input order.</param>
/// <param name="DistinctFullPaths">Resolved files, deduplicated by full path.</param>
public sealed record SourceRefResolution(
    IReadOnlyList<ResolvedSourceRef> Resolved,
    IReadOnlyList<MissingSourceRef> Missing,
    IReadOnlyList<string> DistinctFullPaths)
{
    public bool AnyResolved => Resolved.Count > 0;
}

/// <param name="Ref">The ref exactly as written in the test.</param>
/// <param name="RelativePath">Repo-relative path with <c>/</c> separators.</param>
/// <param name="FullPath">Absolute path of the file.</param>
public sealed record ResolvedSourceRef(string Ref, string RelativePath, string FullPath);

/// <param name="Ref">The ref exactly as written in the test.</param>
public sealed record MissingSourceRef(string Ref, MissingReason Reason);

public enum MissingReason
{
    /// <summary>Nothing left after stripping the fragment (e.g. <c>""</c> or <c>#Section</c>).</summary>
    Empty,
    /// <summary>No candidate file exists inside the repository.</summary>
    NotFound,
    /// <summary>Every candidate lies outside the repository root.</summary>
    OutsideRepository
}
