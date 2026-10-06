using System.Globalization;
using System.Text;
using Spectra.Core.Baseline;
using Spectra.Core.Coverage;
using Spectra.Core.Git;

namespace Spectra.CLI.Baseline;

/// <summary>A ref's content at the bootstrap point: its fingerprint and snapshot text, or why it stays unbaselined.</summary>
public sealed record HistoricRef(string Ref, string? Fingerprint, string? Normalised, string? Reason, bool CurrentContent);

/// <summary>
/// Spec 086 (D7, FR-015–FR-017, research R7): reads refs' sections as they were at a bootstrap point — per owner the
/// last commit at or before a date, or one owner pinned to a commit. The path at the point follows the renames since
/// (newest to oldest); the content comes from one <c>git cat-file --batch</c> process per owner. Docs of owners
/// without a point (no repository, or not the pinned owner) use current content, flagged so the caller can warn.
/// </summary>
public sealed class HistoryBootstrap
{
    public const string ErrorGitRefUnavailable = "GIT_REF_UNAVAILABLE";

    private readonly BaselineContext _context;
    private readonly SourceRefResolver _refs;
    private readonly OwnerResolver _owners;
    private readonly DateTimeOffset? _date;
    private readonly GitOwner? _pinnedOwner;
    private readonly string? _pinnedCommit;
    private readonly Dictionary<string, (string? Commit, string? Reason)> _points = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<GitRename>> _renamesSince = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HistoricRef> _results = new(StringComparer.Ordinal);

    private HistoryBootstrap(
        BaselineContext context, SourceRefResolver refs, OwnerResolver owners, DateTimeOffset? date, GitOwner? pinnedOwner,
        string? pinnedCommit)
    {
        _context = context;
        _refs = refs;
        _owners = owners;
        _date = date;
        _pinnedOwner = pinnedOwner;
        _pinnedCommit = pinnedCommit;
    }

    /// <summary>Docs whose content was read from the working tree, because their owner has no bootstrap point.</summary>
    public SortedSet<string> CurrentContentDocs { get; } = new(StringComparer.Ordinal);

    /// <summary>Bootstrap from each owner's last commit at or before <paramref name="date"/>.</summary>
    public static HistoryBootstrap FromDate(BaselineContext context, SourceRefResolver refs, DateTimeOffset date) =>
        new(context, refs, GitAccess.CreateOwnerResolver(), date, null, null);

    /// <summary>
    /// Bootstrap with the repository containing <paramref name="ownerPath"/> pinned to <paramref name="rev"/>. Throws
    /// <see cref="BaselineStoreException"/> (<see cref="ErrorGitRefUnavailable"/>) when the owner isn't a readable
    /// repository or the revision doesn't resolve in it.
    /// </summary>
    public static async Task<HistoryBootstrap> FromRefAsync(
        BaselineContext context, SourceRefResolver refs, string ownerPath, string rev, CancellationToken ct)
    {
        var owners = GitAccess.CreateOwnerResolver();
        var full = Path.GetFullPath(Path.Combine(refs.RepoRoot, ownerPath));
        if (!Directory.Exists(full))
        {
            throw new BaselineStoreException(ErrorGitRefUnavailable, $"--owner {ownerPath} is not a folder.");
        }

        var (owner, reason) = await owners.OwnerOfDirectoryAsync(full, ct);
        if (owner is null)
        {
            throw new BaselineStoreException(ErrorGitRefUnavailable, $"--owner {ownerPath} is not a readable git repository ({reason}).");
        }

        var result = await owners.Reader.RunAsync(owner.Root, "rev-parse", ["--verify", "--quiet", $"{rev}^{{commit}}"], null, ct);
        var commit = result.Ok ? result.Text.Trim() : null;
        if (string.IsNullOrEmpty(commit))
        {
            throw new BaselineStoreException(ErrorGitRefUnavailable,
                $"--from-ref {rev} does not name a commit in {BaselineStore.Relative(refs.RepoRoot, owner.Root)}.");
        }

        return new HistoryBootstrap(context, refs, owners, null, owner, commit);
    }

    /// <summary>
    /// Computes the historic content of <paramref name="sourceRefs"/> (distinct; call once per command with every ref
    /// that needs a baseline). Results are read with <see cref="Result"/>.
    /// </summary>
    public async Task PrepareAsync(IEnumerable<string> sourceRefs, CancellationToken ct)
    {
        var requests = new Dictionary<GitOwner, List<(string Ref, string Commit, string Path, string? Anchor)>>();
        foreach (var sourceRef in sourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            if (_results.ContainsKey(sourceRef))
            {
                continue;
            }

            var located = await LocateAsync(sourceRef, ct);
            if (located.Result is { } done)
            {
                _results[sourceRef] = done;
                continue;
            }

            var (owner, commit, path) = (located.Owner!, located.Commit!, located.Path!);
            if (!requests.TryGetValue(owner, out var list))
            {
                requests[owner] = list = [];
            }

            list.Add((sourceRef, commit, path, SectionExtractor.AnchorOf(sourceRef)));
        }

        foreach (var (owner, list) in requests)
        {
            var contents = await CatFileAsync(owner, list.Select(r => $"{r.Commit}:{r.Path}").ToList(), ct);
            for (var i = 0; i < list.Count; i++)
            {
                var (sourceRef, commit, _, anchor) = list[i];
                var shortHash = commit[..Math.Min(7, commit.Length)];
                if (contents is null)
                {
                    _results[sourceRef] = new HistoricRef(sourceRef, null, null, "git failed: cat-file", false);
                }
                else if (contents[i] is not { } text)
                {
                    _results[sourceRef] = new HistoricRef(sourceRef, null, null, $"file did not exist at {shortHash}", false);
                }
                else
                {
                    var lookup = SectionExtractor.Extract(text, anchor);
                    _results[sourceRef] = lookup.Outcome == SectionOutcome.AnchorNotFound
                        ? new HistoricRef(sourceRef, null, null, $"anchor did not exist at {shortHash}", false)
                        : new HistoricRef(sourceRef, SourceFingerprint.OfSection(lookup.RawText!),
                            SectionExtractor.ComparisonText(lookup.RawText!), null, false);
                }
            }
        }
    }

    public HistoricRef Result(string sourceRef) => _results[sourceRef];

    private sealed record Located(HistoricRef? Result, GitOwner? Owner, string? Commit, string? Path);

    /// <summary>Where a ref's file was at the bootstrap point, or its result when no history is read for it.</summary>
    private async Task<Located> LocateAsync(string sourceRef, CancellationToken ct)
    {
        var section = await _context.GetSectionAsync(sourceRef, ct);
        IReadOnlyList<RefLocation> locations = section.FullPath is not null
            ? [new RefLocation(section.FullPath, RefLocationKind.RepoRelative)]
            : section.Current.MissingReason == BaselineContext.NotFound ? _refs.CandidateLocations(sourceRef) : [];
        if (locations.Count == 0)
        {
            return new Located(new HistoricRef(sourceRef, null, null, section.Current.MissingReason ?? BaselineContext.NotFound, false), null, null, null);
        }

        foreach (var location in locations)
        {
            var (owner, _) = await _owners.OwnerOfAsync(location.FullPath, ct);
            var point = owner is null ? (null, null) : await PointAsync(owner, ct);
            if (owner is null || point.Commit is null)
            {
                if (point.Reason is { } reason)
                {
                    return new Located(new HistoricRef(sourceRef, null, null, reason, false), null, null, null);
                }

                continue; // no repository, or not the pinned owner: try the next location, else current content
            }

            var since = await RenamesSinceAsync(owner, point.Commit, ct);
            var path = RenameHistory.PathAt(since, OwnerResolver.RelativePath(owner, location.FullPath));
            return new Located(null, owner, point.Commit, path);
        }

        // No owner with a point: current content (FR-016 / Assumptions), if the ref resolves now.
        if (section.Current.Missing)
        {
            return new Located(new HistoricRef(sourceRef, null, null, $"{BaselineContext.NotFound}", false), null, null, null);
        }

        CurrentContentDocs.Add(section.RelativePath!);
        return new Located(new HistoricRef(sourceRef, section.Current.Fingerprint, section.Current.Normalised, null, true), null, null, null);
    }

    /// <summary>The owner's bootstrap commit (null when the owner has no point), or the reason its refs stay unbaselined.</summary>
    private async Task<(string? Commit, string? Reason)> PointAsync(GitOwner owner, CancellationToken ct)
    {
        if (_points.TryGetValue(owner.Root, out var cached))
        {
            return cached;
        }

        (string? Commit, string? Reason) point;
        if (_pinnedOwner is not null)
        {
            point = string.Equals(owner.Root, _pinnedOwner.Root, StringComparison.OrdinalIgnoreCase) ? (_pinnedCommit, null) : (null, null);
        }
        else
        {
            var before = _date!.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            var result = await _owners.Reader.RunAsync(owner.Root, "log", ["-1", "--format=%H", $"--before={before}", "HEAD"], null, ct);
            var commit = result.Ok ? result.Text.Trim() : string.Empty;
            point = commit.Length > 0
                ? (commit, null)
                : (null, result.Ok ? $"no commit at or before {FormatDate(_date.Value)}" : result.Unavailable);
        }

        _points[owner.Root] = point;
        return point;
    }

    private async Task<IReadOnlyList<GitRename>> RenamesSinceAsync(GitOwner owner, string commit, CancellationToken ct)
    {
        if (!_renamesSince.TryGetValue(owner.Root, out var renames))
        {
            var result = await _owners.Reader.RunAsync(owner.Root, "log",
                ["-l0", "-M90%", "--diff-filter=R", "--name-status", "-z", "--format=%x1e%H", $"{commit}..HEAD"], null, ct);
            renames = result.Ok ? RenameHistory.Parse(result.Stdout) : [];
            _renamesSince[owner.Root] = renames;
        }

        return renames;
    }

    /// <summary>One <c>cat-file --batch</c> for every request: each blob's text, or null when it didn't exist.</summary>
    private async Task<List<string?>?> CatFileAsync(GitOwner owner, IReadOnlyList<string> requests, CancellationToken ct)
    {
        var stdin = Encoding.UTF8.GetBytes(string.Concat(requests.Select(r => r + "\n")));
        var result = await _owners.Reader.RunAsync(owner.Root, "cat-file", ["--batch"], stdin, ct);
        if (!result.Ok)
        {
            return null;
        }

        var output = result.Stdout;
        var contents = new List<string?>(requests.Count);
        var at = 0;
        foreach (var _ in requests)
        {
            var newline = Array.IndexOf(output, (byte)'\n', at);
            if (newline < 0)
            {
                return null;
            }

            var header = Encoding.UTF8.GetString(output, at, newline - at).Split(' ');
            at = newline + 1;
            if (header.Length < 3 || header[^1] == "missing" || !long.TryParse(header[2], out var size))
            {
                contents.Add(null);
                continue;
            }

            contents.Add(header[1] == "blob" ? Encoding.UTF8.GetString(output, at, (int)size) : null);
            at += (int)size + 1; // the blob and its trailing newline
        }

        return contents;
    }

    /// <summary>Parses an ISO 8601 date or date-time. A date without a time means the end of that day (UTC), inclusive.</summary>
    public static bool TryParseDate(string? text, out DateTimeOffset date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            date = new DateTimeOffset(day.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero);
            return true;
        }

        return DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out date) && text.Trim().Length >= 10 && char.IsAsciiDigit(text.Trim()[0]);
    }

    private static string FormatDate(DateTimeOffset date) => date.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
