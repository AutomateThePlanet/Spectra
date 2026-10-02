using Spectra.CLI.Criteria;
using Spectra.Core.Baseline;
using Spectra.Core.Coverage;
using Spectra.Core.Criteria;
using Spectra.Core.Models.Config;
using Spectra.Core.Text;

namespace Spectra.CLI.Baseline;

/// <summary>A ref's current section: where it resolved (if it did) and its content.</summary>
public sealed record RefSection(string Ref, string? RelativePath, string? FullPath, CurrentSection Current, bool Ambiguous);

/// <summary>
/// Spec 084: per-run caches for the baseline writers and triage — the source-ref resolver, file text, sections, the
/// criteria (first occurrence of each ID wins), and snapshots — plus the notes a run reports (ambiguous anchors,
/// duplicate criterion IDs). Each document and the criteria are read once per run.
/// </summary>
public sealed class BaselineContext
{
    private readonly string _repoRoot;
    private readonly SourceRefResolver _resolver;
    private readonly Dictionary<string, string?> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RefSection> _sections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _criteria;
    private readonly Dictionary<string, string?> _snapshots = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _notes = new(StringComparer.Ordinal);

    private BaselineContext(string repoRoot, SourceRefResolver resolver, Dictionary<string, string> criteria)
    {
        _repoRoot = repoRoot;
        _resolver = resolver;
        _criteria = criteria;
    }

    public string RepoRoot => _repoRoot;

    /// <summary>Ambiguous anchors and duplicate criterion IDs seen so far, ordered.</summary>
    public IReadOnlyCollection<string> Notes => _notes;

    public static async Task<BaselineContext> CreateAsync(string repoRoot, SpectraConfig config, CancellationToken ct)
    {
        var sourceDir = config.Source?.LocalDir;
        var resolver = new SourceRefResolver(repoRoot, string.IsNullOrWhiteSpace(sourceDir) ? "docs/" : sourceDir);

        var criteriaDir = config.Coverage?.CriteriaDir;
        var files = await CriteriaStore.ScanFilesAsync(
            Path.GetFullPath(Path.Combine(repoRoot, string.IsNullOrWhiteSpace(criteriaDir) ? "docs/criteria" : criteriaDir)), ct);

        var criteria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, list) in files)
        {
            foreach (var criterion in list.Where(c => !string.IsNullOrWhiteSpace(c.Id)))
            {
                criteria.TryAdd(criterion.Id.Trim(), SourceFingerprint.OfCriterion(criterion.Text));
            }
        }

        var context = new BaselineContext(repoRoot, resolver, criteria);
        foreach (var duplicate in CriteriaDuplicateScanner.Scan(files))
        {
            context._notes.Add($"criterion {duplicate.Id} is defined more than once; the first was used");
        }

        return context;
    }

    /// <summary>The current section of <paramref name="sourceRef"/>, read and extracted once per run.</summary>
    public async Task<RefSection> GetSectionAsync(string sourceRef, CancellationToken ct)
    {
        if (_sections.TryGetValue(sourceRef, out var cached))
        {
            return cached;
        }

        var resolution = _resolver.Resolve([sourceRef]);
        RefSection result;
        if (resolution.Resolved.Count == 0)
        {
            var reason = resolution.Missing[0].Reason switch
            {
                MissingReason.OutsideAllowedRoots => "outside allowed roots",
                MissingReason.Empty => "empty ref",
                _ => "not found"
            };
            result = new RefSection(sourceRef, null, null, CurrentSection.Gone(reason), false);
        }
        else
        {
            var resolved = resolution.Resolved[0];
            var text = await ReadFileAsync(resolved.FullPath, ct);
            if (text is null)
            {
                result = new RefSection(sourceRef, resolved.RelativePath, resolved.FullPath, CurrentSection.Gone("unreadable"), false);
            }
            else
            {
                var lookup = SectionExtractor.Extract(text, SectionExtractor.AnchorOf(sourceRef));
                if (lookup.Outcome == SectionOutcome.AnchorNotFound)
                {
                    result = new RefSection(sourceRef, resolved.RelativePath, resolved.FullPath, CurrentSection.Gone("anchor not found"), false);
                }
                else
                {
                    var raw = lookup.RawText!;
                    result = new RefSection(sourceRef, resolved.RelativePath, resolved.FullPath,
                        CurrentSection.Of(SourceFingerprint.OfSection(raw), TextNormalizer.Normalize(raw)), lookup.Ambiguous);
                    if (lookup.Ambiguous)
                    {
                        _notes.Add($"{sourceRef}: more than one heading shares the anchor key '{lookup.AnchorKey}'; the first was used");
                    }
                }
            }
        }

        _sections[sourceRef] = result;
        return result;
    }

    /// <summary>The headings of a resolved document (empty when unreadable).</summary>
    public async Task<IReadOnlyList<MarkdownHeading>> GetHeadingsAsync(string fullPath, CancellationToken ct)
    {
        var text = await ReadFileAsync(fullPath, ct);
        return text is null ? [] : SectionExtractor.Headings(text);
    }

    /// <summary>Whether criterion <paramref name="id"/> exists, and its current fingerprint.</summary>
    public (bool Exists, string? Fingerprint) GetCriterion(string id) =>
        _criteria.TryGetValue(id.Trim(), out var fingerprint) ? (true, fingerprint) : (false, null);

    public async Task<string?> GetSnapshotAsync(string fingerprint, CancellationToken ct)
    {
        if (!_snapshots.TryGetValue(fingerprint, out var text))
        {
            text = await BaselineStore.ReadSnapshotAsync(_repoRoot, fingerprint, ct);
            _snapshots[fingerprint] = text;
        }

        return text;
    }

    private async Task<string?> ReadFileAsync(string fullPath, CancellationToken ct)
    {
        if (!_files.TryGetValue(fullPath, out var text))
        {
            try
            {
                text = await File.ReadAllTextAsync(fullPath, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                text = null;
            }

            _files[fullPath] = text;
        }

        return text;
    }
}
