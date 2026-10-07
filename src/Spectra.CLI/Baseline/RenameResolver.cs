using Spectra.Core.Coverage;
using Spectra.Core.Git;

namespace Spectra.CLI.Baseline;

/// <summary>
/// A missing ref's relink candidate: the new ref (same anchor, same style) and its target (repo-relative), or the
/// reason there is none (the bare FR-003 / FR-006 reason). Spec 087: <see cref="Submodule"/> is the repo-relative path
/// of the submodule that isn't checked out, when that is the reason.
/// </summary>
public sealed record RelinkCandidate(string Ref, string? NewRef, string? Target, string? Reason, string? Submodule = null);

/// <summary>
/// Spec 086 (D3, FR-005–FR-007, research R3): finds where a missing ref's file was renamed to. The ref is looked up
/// at the same locations <see cref="SourceRefResolver"/> tries, in the same order; the first location whose owner's
/// history gives a candidate wins. Git runs only when asked (lazily), once per owner for the history.
/// </summary>
public sealed class RenameResolver
{
    public const int DefaultMinSimilarity = 90;

    /// <summary>Spec 087 (F1): the reason of a ref into a submodule that isn't checked out (bare; triage prefixes it).</summary>
    public static string SubmoduleReason(string path) => $"submodule not checked out: {path} (run git submodule update --init)";

    /// <summary>
    /// Spec 087 (F1, FR-004a): the triage note and <c>source-baseline --init</c> warning for one submodule.
    /// <paramref name="references"/> counts (test, ref) pairs; <paramref name="tests"/> distinct tests.
    /// </summary>
    public static string SubmoduleNote(string path, int references, int tests) =>
        $"{references} reference(s) from {tests} test(s) point into submodule {path}, which isn't checked out (run git submodule update --init)";

    private readonly SourceRefResolver _refs;
    private readonly Dictionary<string, RelinkCandidate> _cache = new(StringComparer.Ordinal);
    private OwnerResolver? _owners;

    public RenameResolver(SourceRefResolver refs, int minSimilarity, OwnerResolver? owners = null)
    {
        _refs = refs;
        MinSimilarity = minSimilarity;
        _owners = owners;
    }

    public int MinSimilarity { get; }

    public OwnerResolver Owners => _owners ??= GitAccess.CreateOwnerResolver();

    public async Task<RelinkCandidate> CandidateForAsync(string sourceRef, CancellationToken ct)
    {
        if (_cache.TryGetValue(sourceRef, out var cached))
        {
            return cached;
        }

        string? firstReason = null;
        string? ownedReason = null;

        // A reason from a location that found a rename (ambiguous, chain, outside the roots) beats "no rename found".
        void Owned(string? reason)
        {
            if (ownedReason is null || (ownedReason.StartsWith("no rename found", StringComparison.Ordinal)
                    && reason is not null && !reason.StartsWith("no rename found", StringComparison.Ordinal)))
            {
                ownedReason = reason;
            }
        }

        RelinkCandidate? found = null;
        foreach (var location in _refs.CandidateLocations(sourceRef))
        {
            var (owner, why) = await Owners.OwnerOfAsync(location.FullPath, ct);
            if (owner is null)
            {
                firstReason ??= why;
                continue;
            }

            // Spec 087 (F1): a ref into a submodule that isn't checked out is a setup problem; history isn't consulted.
            if (await Owners.UncheckedSubmoduleOfAsync(owner, location.FullPath, ct) is { } submodule)
            {
                var path = Path.GetRelativePath(_refs.RepoRoot, Path.Combine(owner.Root, submodule)).Replace('\\', '/');
                found = new RelinkCandidate(sourceRef, null, null, SubmoduleReason(path), path);
                break;
            }

            var (history, historyWhy) = await Owners.RenameHistoryAsync(owner, MinSimilarity, ct);
            if (history is null)
            {
                Owned(historyWhy);
                continue;
            }

            var (target, targetWhy) = history.ForwardCandidate(
                OwnerResolver.RelativePath(owner, location.FullPath), p => File.Exists(Path.Combine(owner.Root, p)));
            if (target is null)
            {
                Owned(targetWhy);
                continue;
            }

            var targetFull = Path.GetFullPath(Path.Combine(owner.Root, target));
            if (!_refs.IsUnderAllowedRoots(targetFull))
            {
                Owned(RenameHistory.OutsideAllowedRoots);
                continue;
            }

            found = new RelinkCandidate(sourceRef, NewRef(sourceRef, location, targetFull),
                Path.GetRelativePath(_refs.RepoRoot, targetFull).Replace('\\', '/'), null);
            break;
        }

        found ??= new RelinkCandidate(sourceRef, null, null, ownedReason ?? firstReason ?? GitReader.NotARepository);
        _cache[sourceRef] = found;
        return found;
    }

    /// <summary>
    /// FR-007: the new ref keeps the original's base when the target is under it (source-dir-relative stays
    /// source-dir-relative), otherwise it is repo-root-relative; <c>/</c> separators; the <c>#anchor</c> verbatim.
    /// </summary>
    private string NewRef(string sourceRef, RefLocation location, string targetFull)
    {
        var hash = sourceRef.IndexOf('#');
        var anchor = hash >= 0 ? sourceRef[hash..] : string.Empty;
        var path = location.Kind switch
        {
            RefLocationKind.Absolute => targetFull,
            RefLocationKind.SourceDirRelative when _refs.IsUnderSourceDir(targetFull) => Path.GetRelativePath(_refs.SourceDir, targetFull),
            _ => Path.GetRelativePath(_refs.RepoRoot, targetFull)
        };
        return path.Replace('\\', '/') + anchor;
    }
}
