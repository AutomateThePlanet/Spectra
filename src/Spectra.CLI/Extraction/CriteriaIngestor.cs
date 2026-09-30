using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Criteria;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Extraction;

/// <summary>
/// The model-free, fail-loud boundary for agent-extracted acceptance criteria (Spec 054). Mirrors
/// <see cref="Spectra.CLI.Generation.GeneratedTestIngestor"/>: it classifies agent content through
/// the <b>reused-verbatim</b> <see cref="CriteriaExtractor.ClassifyResponse"/> and persists
/// <b>only</b> a genuine <see cref="ExtractionOutcome.Extracted"/> result — through the same
/// per-doc <c>.criteria.yaml</c> writer + criteria-index upsert that <c>AnalyzeHandler</c> uses.
///
/// No model call. <see cref="ExtractionOutcome.EmptyResponse"/> and
/// <see cref="ExtractionOutcome.ParseFailure"/> persist nothing and leave the index byte-for-byte
/// unchanged (FR-006 — no cache poisoning), exactly as <c>AnalyzeHandler.cs:575</c> gates on
/// <c>IsCacheable</c>.
/// </summary>
public sealed class CriteriaIngestor
{
    /// <summary>Spec 079: a returned criterion id is unknown, belongs to another doc, or repeats.</summary>
    public const string ErrorInvalidCriterionId = "INVALID_CRITERION_ID";

    private readonly SpectraConfig _config;

    public CriteriaIngestor(SpectraConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    /// Pure classify with no I/O — delegates to the reused-verbatim
    /// <see cref="CriteriaExtractor.ClassifyResponse"/>. Exposed for token-free unit testing of the
    /// boundary contract.
    /// </summary>
    public static CriteriaIngestResult Classify(string? content, string docPath, string? component)
    {
        var result = CriteriaResponseClassifier.Classify(content, docPath, component);
        return result.Outcome switch
        {
            ExtractionOutcome.Extracted => CriteriaIngestResult.Extracted(result.Criteria),
            ExtractionOutcome.EmptyResponse => CriteriaIngestResult.Failure(
                ExtractionOutcome.EmptyResponse,
                "Agent returned no usable content (empty response)."),
            _ => CriteriaIngestResult.Failure(
                ExtractionOutcome.ParseFailure,
                "The response did not contain a parseable JSON array of criteria."),
        };
    }

    /// <summary>
    /// Classifies <paramref name="agentContent"/> and, on <see cref="ExtractionOutcome.Extracted"/>, matches the
    /// criteria to the doc's previous ones (keeping IDs the model returned or whose normalised text is unchanged),
    /// allocates never-reused IDs for the rest, computes the changeset, and persists — all under the criteria
    /// lock (Spec 079). On any failure nothing is written.
    /// </summary>
    /// <param name="agentContent">The agent's extraction response (expected to contain a JSON array).</param>
    /// <param name="currentDir">Workspace root (where <c>spectra.config.json</c> lives).</param>
    /// <param name="docPath">Source document, as typed; normalised to a repo-relative, forward-slash path.</param>
    /// <param name="component">Component override; defaults to a slug derived from the doc filename.</param>
    /// <param name="docHash">Source-document content hash for the cache entry; null skips the hash.</param>
    /// <param name="dryRun">When true, compute everything (IDs, changeset) but persist nothing.</param>
    public async Task<CriteriaIngestResult> IngestAsync(
        string? agentContent,
        string currentDir,
        string docPath,
        string? component,
        string? docHash,
        bool dryRun,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(docPath);

        var normalized = CriteriaPaths.NormalizeSourceDoc(docPath, currentDir);
        if (!normalized.IsSuccess)
        {
            return CriteriaIngestResult.Rejected(CriteriaPaths.ErrorInvalidDocPath,
                $"The document path must be inside the repository: {docPath}");
        }

        var doc = normalized.Path!;
        var resolvedComponent = string.IsNullOrWhiteSpace(component) ? DeriveComponent(doc) : component;

        var classified = Classify(agentContent, doc, resolvedComponent);

        // FR-006 cache gate: only a genuine extraction is persisted.
        if (!classified.IsSuccess)
            return classified;

        try
        {
            return await CriteriaStore.WithLockAsync(currentDir,
                () => IngestLockedAsync(classified.PersistedCriteria, currentDir, doc, resolvedComponent, docHash, dryRun, ct),
                ct);
        }
        catch (CriteriaStoreException ex)
        {
            return CriteriaIngestResult.Rejected(ex.ErrorCode, ex.Message);
        }
    }

    private async Task<CriteriaIngestResult> IngestLockedAsync(
        IReadOnlyList<AcceptanceCriterion> extracted, string currentDir, string doc, string component,
        string? docHash, bool dryRun, CancellationToken ct)
    {
        var ws = await CriteriaStore.LoadAsync(_config, currentDir, ct);

        // FR-016b: write paths migrate the old layout first (inside the same lock).
        MigrationReport? migration = null;
        if (!dryRun && CriteriaPaths.IsOldLayout(ws.Index))
        {
            var report = await CriteriaLayoutMigrator.MigrateAsync(ws, dryRun: false, ct);
            if (!report.AlreadyMigrated)
            {
                migration = report;
                ws = await CriteriaStore.LoadAsync(_config, currentDir, ct);
            }
        }

        // Where the previous criteria live, and where this doc's criteria go.
        var source = ws.FindDocumentSource(doc);
        string? previousFile = null;
        var sharedFile = false;
        var target = CriteriaPaths.DerivedFile(doc);
        if (source is not null && !string.IsNullOrWhiteSpace(source.File))
        {
            previousFile = CriteriaPaths.NormalizeLexical(source.File);
            sharedFile = CriteriaProvenance.IsShared(ws.Index, source);
            if (!sharedFile)
            {
                target = previousFile;
            }
        }

        if (ws.FileOwnedByOther(target, source) is { } owner)
        {
            return CriteriaIngestResult.Rejected(CriteriaPaths.ErrorFileCollision,
                $"Criteria file '{target}' already belongs to '{owner.SourceDoc ?? owner.File}'.");
        }

        var previous = previousFile is null ? [] : await ws.ReadStrictAsync(previousFile, ct);
        if (sharedFile)
        {
            // A past base-name collision: only the criteria this doc wrote are its own (per-criterion provenance).
            previous = CriteriaProvenance.OwnedBy(previous, doc);
        }

        // FR-004/FR-005: validate returned IDs, then the one-to-one normalised-text fallback.
        var incoming = extracted
            .Select(c => new IncomingCriterion(c, string.IsNullOrWhiteSpace(c.Id) ? null : c.Id))
            .ToList();
        var match = CriteriaMatcher.Match(incoming, previous);
        if (!match.IsValid)
        {
            return CriteriaIngestResult.Rejected(ErrorInvalidCriterionId,
                "The response uses criterion id(s) that are not this document's existing criteria, or repeats them: "
                + string.Join(", ", match.InvalidIds));
        }

        // FR-007: new IDs from the committed per-prefix high-water mark (never reused).
        var unmatched = match.Items.Where(i => i.Previous is null).ToList();
        var newIds = CriteriaIdAllocator.Allocate(
            ws.Index, $"AC-{component.ToUpperInvariant()}", unmatched.Count, ws.AllIds);
        for (var i = 0; i < unmatched.Count; i++)
        {
            unmatched[i].Criterion.Id = newIds[i];
        }

        var next = match.Items.Select(i => i.Criterion).ToList();
        foreach (var criterion in next)
        {
            criterion.SourceDoc = doc;
            criterion.SourceType = "document";
            criterion.Component = component;
        }

        var (testsById, warnings) = await CriteriaReferenceCounter.LoadAsync(currentDir, _config, ct);
        var changeset = CriteriaChangeset.Build(doc, previous, next).WithReferences(testsById);

        // FR-014 no-op rule: nothing changed and the index already records this exact state → write nothing.
        var hashSame = source is not null && string.Equals(source.DocHash, docHash, StringComparison.OrdinalIgnoreCase);
        var entryCurrent = source is not null
            && string.Equals(source.SourceDoc, doc, StringComparison.Ordinal)
            && string.Equals(CriteriaPaths.NormalizeLexical(source.File), target, StringComparison.Ordinal);
        var needFileWrite = !changeset.IsEmpty || previousFile != target || !File.Exists(ws.FullPath(target));
        var needIndexWrite = needFileWrite || !hashSame || !entryCurrent;

        var written = false;
        if (!dryRun && (needFileWrite || needIndexWrite))
        {
            if (needFileWrite)
            {
                await ws.WriteCriteriaAsync(target, next, doc, docHash, ct);
            }

            if (source is null)
            {
                source = new CriteriaSource { SourceType = "document" };
                ws.Index.Sources.Add(source);
            }

            source.File = target;
            source.SourceDoc = doc;
            source.DocHash = docHash;
            source.CriteriaCount = next.Count;
            source.LastExtracted = DateTime.UtcNow;
            source.Outcome = "extracted";
            await ws.SaveIndexAsync(ct);

            // Spec 080 B2: leaving a shared (collided) file takes this doc's criteria out of it, so they are not
            // counted twice by the recursive criteria scanners; the file goes once it is empty and unreferenced.
            if (sharedFile && previousFile is not null && previousFile != target)
            {
                await ws.LeaveSharedFileAsync(previousFile, doc, ct);
            }

            written = true;
        }

        return CriteriaIngestResult.Extracted(next) with
        {
            Doc = doc,
            Changeset = changeset,
            Written = written,
            DuplicateIds = ws.Duplicates,
            Migration = migration,
            Warnings = warnings
        };
    }

    /// <summary>Best-effort source-document hash for the cache entry; null when unreadable.</summary>
    public static async Task<string?> TryComputeDocHashAsync(string docFullPath, CancellationToken ct)
    {
        try
        {
            return await FileHasher.ComputeFileHashAsync(docFullPath, ct);
        }
        catch
        {
            return null;
        }
    }

    private static string DeriveComponent(string docPath) =>
        Path.GetFileNameWithoutExtension(docPath).Replace(' ', '-').ToLowerInvariant();
}

/// <summary>
/// Outcome of the model-free criteria ingest boundary. On success carries the persisted criteria;
/// on a non-cacheable outcome carries the typed failure and specific messages (the retry payload a
/// Spec 055 skill keys on). Mirrors <see cref="Spectra.CLI.Generation.IngestResult"/>.
/// </summary>
public sealed record CriteriaIngestResult
{
    /// <summary>The classified outcome from <see cref="CriteriaExtractor.ClassifyResponse"/>.</summary>
    public ExtractionOutcome Outcome { get; private init; }

    /// <summary>True when the outcome is <see cref="ExtractionOutcome.Extracted"/> (a rejection is
    /// <see cref="ExtractionOutcome.Rejected"/>, Spec 080 B6).</summary>
    public bool IsSuccess => Outcome == ExtractionOutcome.Extracted;

    /// <summary>Spec 079: machine-readable reason for a rejected ingest (e.g. <c>INVALID_CRITERION_ID</c>).</summary>
    public string? ErrorCode { get; private init; }

    /// <summary>Spec 079: the normalised source doc.</summary>
    public string? Doc { get; init; }

    /// <summary>Spec 079: what changed in this doc's criteria (computed on dry runs too).</summary>
    public CriteriaChangeset? Changeset { get; init; }

    /// <summary>Spec 079: false on a dry run or when the no-op rule wrote nothing.</summary>
    public bool Written { get; init; }

    /// <summary>Spec 079: workspace-wide duplicate criterion IDs (reported, never renumbered).</summary>
    public IReadOnlyList<DuplicateCriterionId> DuplicateIds { get; init; } = [];

    /// <summary>Spec 079: the automatic layout migration this ingest ran first, if any.</summary>
    public MigrationReport? Migration { get; init; }

    /// <summary>Non-blocking warnings (e.g. an unreadable test index while counting references).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Criteria persisted (or, on dry-run, that would be). Empty on failure.</summary>
    public IReadOnlyList<AcceptanceCriterion> PersistedCriteria { get; private init; } = [];

    /// <summary>Specific, actionable messages. Empty on success.</summary>
    public IReadOnlyList<string> Errors { get; private init; } = [];

    private CriteriaIngestResult() { }

    /// <summary>Creates a successful (extracted) result.</summary>
    public static CriteriaIngestResult Extracted(IReadOnlyList<AcceptanceCriterion> criteria) => new()
    {
        Outcome = ExtractionOutcome.Extracted,
        PersistedCriteria = criteria ?? []
    };

    /// <summary>Spec 079: a classified response that was rejected before persisting (nothing written).</summary>
    public static CriteriaIngestResult Rejected(string errorCode, params string[] errors) => new()
    {
        Outcome = ExtractionOutcome.Rejected,
        ErrorCode = errorCode,
        Errors = errors ?? []
    };

    /// <summary>Creates a fail-loud (non-cacheable) result. Persists nothing.</summary>
    public static CriteriaIngestResult Failure(ExtractionOutcome outcome, params string[] errors) => new()
    {
        Outcome = outcome,
        Errors = errors ?? []
    };
}
