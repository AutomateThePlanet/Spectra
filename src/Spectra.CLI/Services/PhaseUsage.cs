namespace Spectra.CLI.Services;

/// <summary>
/// Aggregate of token usage for one (Phase, Model, Provider) tuple (Spec 040).
/// Produced by <see cref="TokenUsageTracker.GetSummary"/>.
///
/// <c>Estimated</c> (Spec 040 follow-up) is true when any contributing call
/// to this aggregate used the <see cref="TokenEstimator"/> fallback instead
/// of provider-reported counts. It propagates via logical OR during
/// aggregation — any estimated call in the phase flags the whole phase
/// (and by extension the run total) as estimated.
/// </summary>
public sealed record PhaseUsage(
    string Phase,
    string Model,
    string Provider,
    int Calls,
    int TokensIn,
    int TokensOut,
    TimeSpan Elapsed,
    bool Estimated = false)
{
    public int TotalTokens => TokensIn + TokensOut;
}
