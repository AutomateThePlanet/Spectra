using Spectra.CLI.Index;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Bridges the v2 documentation-index layout (manifest + suite files) to the
/// existing <see cref="BehaviorAnalyzer.AnalyzeAsync"/> input. Resolves which
/// doc-suites the user's <c>--suite</c>/<c>--focus</c> request implies, filters
/// the loaded source documents accordingly, and runs the
/// <see cref="PreFlightTokenChecker"/> budget check before any AI call.
/// </summary>
/// <remarks>
/// Scope (Spec 040 Phase 4 minimum bug-fix path): the analyzer still receives
/// <c>IReadOnlyList&lt;SourceDocument&gt;</c> as before — this class only narrows
/// that list and emits the actionable error when the request would overflow
/// the model context window. A future Phase swaps the analyzer to
/// manifest-driven loading directly.
/// </remarks>
public sealed class AnalyzerInputBuilder
{
    /// <summary>
    /// Per-document content cap mirrored from <c>BehaviorAnalyzer.FormatDocuments</c>.
    /// </summary>
    private const int PerDocContentCharCap = 2000;

    /// <summary>
    /// Fixed-cost tokens per document (header line, sections list, separators).
    /// Independent of content length.
    /// </summary>
    private const int PerDocumentHeaderTokens = 50;

    /// <summary>
    /// Fixed overhead for the analyzer prompt (template, categories,
    /// technique guidance, focus area, coverage snapshot). Conservative;
    /// prefers fail-fast over silent overflow.
    /// </summary>
    public const int PromptOverheadTokens = 8_000;

    /// <summary>
    /// Worst-case per-document estimate (header + content cap). Used by tests
    /// that don't have access to actual document content.
    /// </summary>
    public const int PerDocumentTokenEstimate = PerDocumentHeaderTokens + PerDocContentCharCap / 4;

    private readonly DocIndexManifestReader _manifestReader = new();
    private readonly SuiteIndexFileReader _suiteReader = new();
    private readonly DocSuiteSelector _selector = new();
    private readonly PreFlightTokenChecker _preflight = new();

    /// <summary>
    /// Resolves the manifest, picks the relevant doc-suites, filters
    /// <paramref name="allDocuments"/> to the selected suites, and enforces
    /// the pre-flight budget. Throws <see cref="PreFlightBudgetExceededException"/>
    /// when the resulting prompt would exceed <paramref name="budgetTokens"/>.
    /// </summary>
    /// <param name="basePath">Repository / project root.</param>
    /// <param name="manifestPath">Path to <c>_manifest.yaml</c>. When the file
    /// is missing (no v2 layout yet), the input is returned unchanged with a
    /// single informational warning — pre-flight still runs against the raw
    /// document estimate.</param>
    /// <param name="indexDir">Path to <c>docs/_index/</c>.</param>
    /// <param name="allDocuments">Pre-loaded source documents (typically from
    /// <c>SourceDocumentLoader.LoadAllAsync</c>).</param>
    /// <param name="suiteFilter">User's <c>--suite</c> arg (or null).</param>
    /// <param name="focusFilter">User's <c>--focus</c> arg (or null).</param>
    /// <param name="budgetTokens">From <c>ai.analysis.max_prompt_tokens</c>.</param>
    /// <param name="includeArchived">User's <c>--include-archived</c> flag.</param>
    public async Task<AnalyzerInputResult> BuildAsync(
        string basePath,
        string manifestPath,
        string indexDir,
        IReadOnlyList<SourceDocument> allDocuments,
        string? suiteFilter,
        string? focusFilter,
        int budgetTokens,
        bool includeArchived,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(allDocuments);

        var warnings = new List<string>();

        var manifest = await _manifestReader.ReadAsync(manifestPath, ct);
        if (manifest is null)
        {
            // No v2 layout yet — pre-flight still runs but only against the
            // raw document estimate.
            EnforcePreflight(allDocuments, Array.Empty<DocSuiteEntry>(), budgetTokens);
            return new AnalyzerInputResult(allDocuments, Array.Empty<DocSuiteEntry>(), warnings);
        }

        var selection = _selector.Select(
            manifest, suiteFilter, focusFilter, budgetTokens, includeArchived);
        warnings.AddRange(selection.Warnings);

        IReadOnlyList<SourceDocument> filtered = allDocuments;
        if (selection.Selected.Count > 0)
        {
            var allowedPaths = await CollectAllowedPathsAsync(
                indexDir, selection.Selected, ct);

            if (allowedPaths.Count > 0)
            {
                filtered = allDocuments
                    .Where(d => allowedPaths.Contains(d.Path))
                    .ToList();
            }
        }

        EnforcePreflight(filtered, selection.Selected, budgetTokens);
        return new AnalyzerInputResult(filtered, selection.Selected, warnings);
    }

    /// <summary>
    /// Estimates the prompt-token cost of an analyzer call given the
    /// filtered document list. Sums each document's actual content length
    /// (capped at <see cref="PerDocContentCharCap"/> to mirror what
    /// <c>BehaviorAnalyzer.FormatDocuments</c> emits) plus a fixed
    /// per-document header cost and the prompt overhead.
    /// </summary>
    public static int EstimatePromptTokens(IReadOnlyCollection<SourceDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var contentTokens = 0;
        foreach (var doc in documents)
        {
            var len = doc.Content?.Length ?? 0;
            var capped = Math.Min(PerDocContentCharCap, len);
            // Spec 040 R-005: TokenEstimator uses the chars/4 heuristic.
            contentTokens += capped / 4 + PerDocumentHeaderTokens;
        }

        return PromptOverheadTokens + contentTokens;
    }

    private void EnforcePreflight(
        IReadOnlyCollection<SourceDocument> filtered,
        IReadOnlyList<DocSuiteEntry> selectedSuites,
        int budgetTokens)
    {
        if (budgetTokens <= 0) return;

        var estimated = EstimatePromptTokens(filtered);
        var suiteEstimates = selectedSuites.Count > 0
            ? selectedSuites
                .Select(s => new SuiteTokenEstimate(s.Id, s.TokensEstimated))
                .ToList()
            : (IReadOnlyList<SuiteTokenEstimate>)Array.Empty<SuiteTokenEstimate>();

        _preflight.EnforceBudget(
            estimatedTokens: estimated,
            budgetTokens: budgetTokens,
            overflowingSuites: suiteEstimates,
            commandHint: "spectra ai generate");
    }

    private async Task<HashSet<string>> CollectAllowedPathsAsync(
        string indexDir,
        IReadOnlyList<DocSuiteEntry> selected,
        CancellationToken ct)
    {
        var allowedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suite in selected)
        {
            var suiteFilePath = Path.Combine(indexDir, suite.IndexFile);
            var suiteFile = await _suiteReader.ReadAsync(suiteFilePath, suite.Id, ct);
            if (suiteFile is null) continue;
            foreach (var entry in suiteFile.Entries)
            {
                allowedPaths.Add(entry.Path);
            }
        }
        return allowedPaths;
    }
}

/// <summary>
/// Result of <see cref="AnalyzerInputBuilder.BuildAsync"/>.
/// </summary>
public sealed class AnalyzerInputResult
{
    public AnalyzerInputResult(
        IReadOnlyList<SourceDocument> filteredDocuments,
        IReadOnlyList<DocSuiteEntry> selectedSuites,
        IReadOnlyList<string> warnings)
    {
        FilteredDocuments = filteredDocuments;
        SelectedSuites = selectedSuites;
        Warnings = warnings;
    }

    /// <summary>The documents to feed into the analyzer.</summary>
    public IReadOnlyList<SourceDocument> FilteredDocuments { get; }

    /// <summary>The doc-suites the documents came from. Empty when no
    /// manifest existed or no filter matched.</summary>
    public IReadOnlyList<DocSuiteEntry> SelectedSuites { get; }

    /// <summary>Non-fatal warnings to surface to the user.</summary>
    public IReadOnlyList<string> Warnings { get; }
}
