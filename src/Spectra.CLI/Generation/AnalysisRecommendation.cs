namespace Spectra.CLI.Generation;

/// <summary>
/// Typed outcome of ingesting an agent's behavior-analysis JSON at the fail-loud
/// boundary (Spec 059). Mirrors the classification style of
/// <see cref="Spectra.CLI.Verification.VerdictIngestResult"/>: a well-formed analysis
/// becomes a <see cref="Recommendation"/>; damage (empty / unparseable) is surfaced as a
/// typed failure carrying a specific error — never coerced into a soft recommendation.
/// </summary>
public enum AnalysisIngestOutcome
{
    /// <summary>A well-formed behavior list was classified into a recommendation.</summary>
    Recommendation,

    /// <summary>The content was empty/whitespace — damage, fail loud (no recommendation).</summary>
    EmptyResponse,

    /// <summary>Parsed to zero behaviors / unparseable JSON — damage, fail loud.</summary>
    ParseFailure
}

/// <summary>
/// Result of <see cref="AnalysisRecommendationBuilder.Build"/> (Spec 059). The deterministic
/// accounting relocated from <c>BehaviorAnalyzer</c> (covered-count dedup, per-category and
/// per-technique breakdowns, recommended-count) is exposed as an advisory recommendation the
/// generation skill presents for approval. Damage is surfaced as a typed failure, never a
/// silent zero-recommendation.
/// </summary>
public sealed record AnalysisRecommendation
{
    /// <summary>The typed classification.</summary>
    public AnalysisIngestOutcome Outcome { get; private init; }

    /// <summary>True only when a well-formed recommendation was produced.</summary>
    public bool IsSuccess => Outcome == AnalysisIngestOutcome.Recommendation;

    /// <summary>Total distinct testable behaviors identified by the agent.</summary>
    public int TotalBehaviors { get; private init; }

    /// <summary>Behaviors already covered by existing tests.</summary>
    public int AlreadyCovered { get; private init; }

    /// <summary>Recommended number of new tests to generate (TotalBehaviors − AlreadyCovered, floored at 0).</summary>
    public int RecommendedCount => Math.Max(0, TotalBehaviors - AlreadyCovered);

    /// <summary>Count per behavior category (null/blank → "uncategorized").</summary>
    public IReadOnlyDictionary<string, int> Breakdown { get; private init; }
        = new Dictionary<string, int>();

    /// <summary>Count per ISTQB technique (blank techniques excluded, upper-invariant keys).</summary>
    public IReadOnlyDictionary<string, int> TechniqueBreakdown { get; private init; }
        = new Dictionary<string, int>();

    /// <summary>Number of source documents the agent analyzed.</summary>
    public int DocumentsAnalyzed { get; private init; }

    /// <summary>Specific error(s) on a damage outcome; empty on success.</summary>
    public IReadOnlyList<string> Errors { get; private init; } = [];

    private AnalysisRecommendation() { }

    /// <summary>Creates a successful recommendation carrying the deterministic accounting.</summary>
    public static AnalysisRecommendation Recommendation(
        int totalBehaviors,
        int alreadyCovered,
        IReadOnlyDictionary<string, int> breakdown,
        IReadOnlyDictionary<string, int> techniqueBreakdown,
        int documentsAnalyzed) => new()
        {
            Outcome = AnalysisIngestOutcome.Recommendation,
            TotalBehaviors = totalBehaviors,
            AlreadyCovered = alreadyCovered,
            Breakdown = breakdown ?? new Dictionary<string, int>(),
            TechniqueBreakdown = techniqueBreakdown ?? new Dictionary<string, int>(),
            DocumentsAnalyzed = documentsAnalyzed
        };

    /// <summary>Creates a fail-loud empty-response result with specific error(s).</summary>
    public static AnalysisRecommendation Empty(params string[] errors) => new()
    {
        Outcome = AnalysisIngestOutcome.EmptyResponse,
        Errors = errors ?? []
    };

    /// <summary>Creates a fail-loud parse-failure result with specific error(s).</summary>
    public static AnalysisRecommendation ParseFail(params string[] errors) => new()
    {
        Outcome = AnalysisIngestOutcome.ParseFailure,
        Errors = errors ?? []
    };
}
