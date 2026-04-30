using Spectra.Core.Models.Index;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Maps a user's <c>--suite</c> / <c>--focus</c> / no-filter request to the
/// concrete list of <see cref="DocSuiteEntry"/> objects whose index files the
/// caller should load. Per research.md R-011 (Spec 040).
/// </summary>
/// <remarks>
/// Resolution priority:
/// <list type="number">
///   <item>Exact <c>--suite &lt;id&gt;</c> match → only that suite.</item>
///   <item>Unknown <c>--suite</c> → emit warning, fall back to no-filter.</item>
///   <item><c>--focus "&lt;keywords&gt;"</c> → keyword overlap with id+title;
///     pick top suites until cumulative tokens ≈ 70% of budget.</item>
///   <item>No filter → all suites where <c>skip_analysis == false</c>
///     (or all when <c>includeArchived</c>), in priority order:
///     highest-token-density first.</item>
/// </list>
/// </remarks>
public sealed class DocSuiteSelector
{
    /// <summary>
    /// The portion of the configured budget that <c>--focus</c> packing aims
    /// to fill before stopping (header room for response + template).
    /// </summary>
    private const double FocusPackTargetFraction = 0.70;

    /// <summary>
    /// Resolves which suites to load from the manifest given the user's filters.
    /// </summary>
    /// <param name="manifest">Loaded manifest. Must not be null.</param>
    /// <param name="suiteFilter">Value of <c>--suite</c> (case-sensitive exact
    /// match against <see cref="DocSuiteEntry.Id"/>). Null when not specified.</param>
    /// <param name="focusFilter">Value of <c>--focus</c>. Null when not specified.</param>
    /// <param name="budgetTokens">Pre-flight budget (e.g., 96,000). When 0 or
    /// negative, focus packing is disabled and all candidate suites are
    /// returned in priority order.</param>
    /// <param name="includeArchived">When true, includes suites with
    /// <c>SkipAnalysis == true</c> in the candidate set.</param>
    public DocSuiteSelectionResult Select(
        DocIndexManifest manifest,
        string? suiteFilter,
        string? focusFilter,
        int budgetTokens,
        bool includeArchived = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var warnings = new List<string>();

        // 1. Exact --suite match.
        if (!string.IsNullOrWhiteSpace(suiteFilter))
        {
            var exact = manifest.Groups.FirstOrDefault(g =>
                string.Equals(g.Id, suiteFilter, StringComparison.Ordinal));
            if (exact is not null)
            {
                return new DocSuiteSelectionResult(new[] { exact }, warnings);
            }

            warnings.Add(
                $"No doc-suite '{suiteFilter}' in manifest. Available doc-suites: " +
                string.Join(", ", manifest.Groups.Select(g => g.Id)) +
                ". Pass --doc-suite <id> to filter analyzer input independently of the test-suite name " +
                "(e.g., --doc-suite cm_ug_topics for a 'central-manager-department' test suite).");
            // Fall through to no-filter behavior.
        }

        // Candidate set: respects skip_analysis unless includeArchived.
        var candidates = manifest.Groups
            .Where(g => includeArchived || !g.SkipAnalysis)
            .ToList();

        if (candidates.Count == 0)
        {
            return new DocSuiteSelectionResult(Array.Empty<DocSuiteEntry>(), warnings);
        }

        // 3. --focus keyword scoring with token-budget packing.
        if (!string.IsNullOrWhiteSpace(focusFilter) && budgetTokens > 0)
        {
            var keywords = focusFilter
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => k.ToLowerInvariant())
                .Where(k => k.Length > 2)
                .ToList();

            if (keywords.Count > 0)
            {
                var scored = candidates
                    .Select(g => new
                    {
                        Suite = g,
                        Score = ScoreSuite(g, keywords),
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Suite.TokensEstimated)
                    .ToList();

                if (scored.Count > 0)
                {
                    var target = (int)(budgetTokens * FocusPackTargetFraction);
                    var picked = new List<DocSuiteEntry>();
                    var running = 0;
                    foreach (var x in scored)
                    {
                        if (running > 0 && running + x.Suite.TokensEstimated > target) break;
                        picked.Add(x.Suite);
                        running += x.Suite.TokensEstimated;
                    }
                    if (picked.Count == 0)
                    {
                        // First match alone exceeds the target — still pick it;
                        // the pre-flight check will surface the budget violation.
                        picked.Add(scored[0].Suite);
                    }
                    return new DocSuiteSelectionResult(picked, warnings);
                }

                warnings.Add(
                    $"No suite matched --focus keywords. Loading all non-archived suites.");
            }
        }

        // 4. No filter: all non-skip suites, sorted highest-tokens-first
        //    (priority-ordered packing — larger product areas are more likely
        //    to be the user's primary target on a first run).
        var ordered = candidates
            .OrderByDescending(g => g.TokensEstimated)
            .ThenBy(g => g.Id, StringComparer.Ordinal)
            .ToList();

        return new DocSuiteSelectionResult(ordered, warnings);
    }

    private static int ScoreSuite(DocSuiteEntry suite, IReadOnlyList<string> keywords)
    {
        var haystack = $"{suite.Id} {suite.Title}".ToLowerInvariant();
        var hits = 0;
        foreach (var keyword in keywords)
        {
            if (haystack.Contains(keyword, StringComparison.Ordinal))
            {
                hits++;
            }
        }
        return hits;
    }
}

/// <summary>
/// Result of <see cref="SuiteSelector.Select"/>.
/// </summary>
public sealed class DocSuiteSelectionResult
{
    public DocSuiteSelectionResult(
        IReadOnlyList<DocSuiteEntry> selected,
        IReadOnlyList<string> warnings)
    {
        Selected = selected;
        Warnings = warnings;
    }

    /// <summary>
    /// Suites the caller should load, in priority order. May be empty when
    /// the manifest has no candidate suites under the current filter.
    /// </summary>
    public IReadOnlyList<DocSuiteEntry> Selected { get; }

    /// <summary>
    /// Non-fatal warnings (e.g., unknown <c>--suite</c>, no <c>--focus</c>
    /// match). Surface to the user before continuing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }
}
