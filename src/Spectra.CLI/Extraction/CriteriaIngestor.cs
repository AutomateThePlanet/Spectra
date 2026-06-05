using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Infrastructure;
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
        var result = CriteriaExtractor.ClassifyResponse(content, docPath, component);
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
    /// Classifies <paramref name="agentContent"/> and, on <see cref="ExtractionOutcome.Extracted"/>,
    /// persists the criteria into <c>{component}.criteria.yaml</c> and upserts the criteria index.
    /// On any non-cacheable outcome nothing is written.
    /// </summary>
    /// <param name="agentContent">The agent's extraction response (expected to contain a JSON array).</param>
    /// <param name="currentDir">Workspace root (where <c>spectra.config.json</c> lives).</param>
    /// <param name="docPath">Source document path the criteria belong to (sets <c>SourceDoc</c>).</param>
    /// <param name="component">Component override; defaults to a slug derived from the doc filename.</param>
    /// <param name="docHash">Source-document content hash for the cache entry; null skips the hash.</param>
    /// <param name="dryRun">When true, classify + report but persist nothing.</param>
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

        var resolvedComponent = string.IsNullOrWhiteSpace(component)
            ? DeriveComponent(docPath)
            : component;

        var classified = Classify(agentContent, docPath, resolvedComponent);

        // FR-006 cache gate: only a genuine extraction is persisted. Anything else returns the
        // fail-loud result with the index untouched.
        if (!classified.IsSuccess)
            return classified;

        if (dryRun)
            return classified; // classified Extracted, but persist nothing on a dry run

        var criteria = classified.PersistedCriteria;

        var criteriaDir = Path.Combine(currentDir, _config.Coverage.CriteriaDir);
        var criteriaIndexPath = Path.Combine(currentDir, _config.Coverage.CriteriaFile);
        Directory.CreateDirectory(criteriaDir);

        var docBaseName = Path.GetFileNameWithoutExtension(docPath);
        var criteriaFileName = $"{docBaseName}.criteria.yaml";
        var criteriaFilePath = Path.Combine(criteriaDir, criteriaFileName);

        // Assign/reuse IDs against the existing per-doc file (same scheme as AnalyzeHandler).
        var reader = new CriteriaFileReader();
        var existingCriteria = await reader.ReadAsync(criteriaFilePath, ct);
        var existingIdMap = existingCriteria
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .GroupBy(c => c.Text, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var componentPrefix = $"AC-{resolvedComponent.ToUpperInvariant()}-";
        var maxId = existingCriteria
            .Where(c => c.Id.StartsWith(componentPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(c => int.TryParse(c.Id[componentPrefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        var nextId = maxId + 1;

        foreach (var criterion in criteria)
        {
            if (existingIdMap.TryGetValue(criterion.Text, out var existingId))
            {
                criterion.Id = existingId;
            }
            else
            {
                criterion.Id = $"{componentPrefix}{nextId:D3}";
                nextId++;
            }
            criterion.SourceDoc = docPath;
            criterion.SourceType = "document";
            criterion.Component = resolvedComponent;
        }

        // Write the per-doc criteria file.
        var fileWriter = new CriteriaFileWriter();
        await fileWriter.WriteAsync(criteriaFilePath, criteria, docPath, docHash, ct);

        // Upsert the index source (Spec 048: only genuine extractions reach here ⇒ outcome=extracted).
        var indexReader = new CriteriaIndexReader();
        var index = await indexReader.ReadAsync(criteriaIndexPath, ct);
        var existingSource = index.Sources
            .FirstOrDefault(s => string.Equals(s.SourceDoc, docPath, StringComparison.OrdinalIgnoreCase));
        if (existingSource is not null)
        {
            existingSource.DocHash = docHash;
            existingSource.CriteriaCount = criteria.Count;
            existingSource.LastExtracted = DateTime.UtcNow;
            existingSource.File = criteriaFileName;
            existingSource.Outcome = "extracted";
        }
        else
        {
            index.Sources.Add(new CriteriaSource
            {
                File = criteriaFileName,
                SourceDoc = docPath,
                SourceType = "document",
                DocHash = docHash,
                CriteriaCount = criteria.Count,
                LastExtracted = DateTime.UtcNow,
                Outcome = "extracted"
            });
        }

        var indexWriter = new CriteriaIndexWriter();
        await indexWriter.WriteAsync(criteriaIndexPath, index, ct);

        return classified;
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

    /// <summary>True when the outcome is <see cref="ExtractionOutcome.Extracted"/>.</summary>
    public bool IsSuccess => Outcome == ExtractionOutcome.Extracted;

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

    /// <summary>Creates a fail-loud (non-cacheable) result. Persists nothing.</summary>
    public static CriteriaIngestResult Failure(ExtractionOutcome outcome, params string[] errors) => new()
    {
        Outcome = outcome,
        Errors = errors ?? []
    };
}
