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
    private readonly Dictionary<string, string> _criteriaText;
    private readonly Dictionary<string, string?> _snapshots = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _notes = new(StringComparer.Ordinal);
    private RenameResolver? _renames;

    /// <summary>The missing reason of a ref whose file doesn't exist (the only one rename detection applies to).</summary>
    public const string NotFound = "not found";

    private BaselineContext(
        string repoRoot, SourceRefResolver resolver, Dictionary<string, string> criteria, Dictionary<string, string> criteriaText)
    {
        _repoRoot = repoRoot;
        _resolver = resolver;
        _criteria = criteria;
        _criteriaText = criteriaText;
    }

    public string RepoRoot => _repoRoot;

    /// <summary>The source-ref resolver (repo root and source dir).</summary>
    public SourceRefResolver Refs => _resolver;

    /// <summary>Spec 086: the rename resolver at the default 90% threshold, created on first use (git runs only then).</summary>
    public RenameResolver Renames => _renames ??= new RenameResolver(_resolver, RenameResolver.DefaultMinSimilarity);

    /// <summary>Spec 086: a rename resolver at <paramref name="minSimilarity"/>% (<c>relink-sources --min-similarity</c>).</summary>
    public RenameResolver CreateRenameResolver(int minSimilarity) =>
        minSimilarity == RenameResolver.DefaultMinSimilarity ? Renames : new RenameResolver(_resolver, minSimilarity);

    /// <summary>Adds a note to the run's notes (ordered, de-duplicated).</summary>
    public void AddNote(string note) => _notes.Add(note);

    /// <summary>
    /// Spec 087 (F1, FR-004a): one line per submodule that isn't checked out, counting every missing ref of
    /// <paramref name="tests"/> that points into it — (test, ref) pairs, baselined or not — and the distinct tests.
    /// Ordered by submodule path.
    /// </summary>
    public async Task<IReadOnlyList<string>> SubmoduleNotesAsync(IEnumerable<Spectra.Core.Models.TestCase> tests, CancellationToken ct)
    {
        var references = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var testIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
            {
                if (await RelinkCandidateAsync(sourceRef, Renames, ct) is { Submodule: { } submodule })
                {
                    references[submodule] = references.GetValueOrDefault(submodule) + 1;
                    if (!testIds.TryGetValue(submodule, out var ids)) testIds[submodule] = ids = new HashSet<string>(StringComparer.Ordinal);
                    ids.Add(test.Id);
                }
            }
        }

        return references.Select(r => RenameResolver.SubmoduleNote(r.Key, r.Value, testIds[r.Key].Count)).ToList();
    }

    /// <summary>
    /// Spec 086: the relink candidate of <paramref name="sourceRef"/> when its file is missing (reason
    /// <see cref="NotFound"/>), else null — a resolving ref, or one missing for another reason, is never relinked.
    /// </summary>
    public async Task<RelinkCandidate?> RelinkCandidateAsync(string sourceRef, RenameResolver renames, CancellationToken ct)
    {
        var section = await GetSectionAsync(sourceRef, ct);
        return section.Current.Missing && section.Current.MissingReason == NotFound
            ? await renames.CandidateForAsync(sourceRef, ct)
            : null;
    }

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
        var criteriaText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, list) in files)
        {
            foreach (var criterion in list.Where(c => !string.IsNullOrWhiteSpace(c.Id)))
            {
                if (criteria.TryAdd(criterion.Id.Trim(), SourceFingerprint.OfCriterion(criterion.Text)))
                {
                    criteriaText[criterion.Id.Trim()] = criterion.Text;
                }
            }
        }

        var context = new BaselineContext(repoRoot, resolver, criteria, criteriaText);
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
                _ => NotFound
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
                        CurrentSection.FromRaw(raw), lookup.Ambiguous);
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

    /// <summary>
    /// Spec 088 (FR-001): the raw text of <paramref name="sourceRef"/>'s section as it is now — the 085 matcher over the
    /// file; the whole file for an anchor-less ref — or null when the ref doesn't resolve or its anchor matches no
    /// heading. Uses the same resolution and file cache as <see cref="GetSectionAsync"/>.
    /// </summary>
    public async Task<string?> GetRawSectionAsync(string sourceRef, CancellationToken ct)
    {
        var section = await GetSectionAsync(sourceRef, ct);
        if (section.Current.Missing || section.FullPath is null)
        {
            return null;
        }

        var text = await ReadFileAsync(section.FullPath, ct);
        if (text is null)
        {
            return null;
        }

        var lookup = SectionExtractor.Extract(text, SectionExtractor.AnchorOf(sourceRef));
        return lookup.Outcome == SectionOutcome.AnchorNotFound ? null : lookup.RawText;
    }

    /// <summary>Spec 088 (FR-005): a criterion's current text (the first definition of the ID), or null when it doesn't exist.</summary>
    public string? GetCriterionText(string id) => _criteriaText.TryGetValue(id.Trim(), out var text) ? text : null;

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
