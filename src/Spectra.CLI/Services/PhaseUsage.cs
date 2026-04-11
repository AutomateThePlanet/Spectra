namespace Spectra.CLI.Services;

/// <summary>
/// Aggregate of token usage for one (Phase, Model, Provider) tuple (Spec 040).
/// Produced by <see cref="TokenUsageTracker.GetSummary"/>.
/// </summary>
public sealed record PhaseUsage(
    string Phase,
    string Model,
    string Provider,
    int Calls,
    int TokensIn,
    int TokensOut,
    TimeSpan Elapsed)
{
    public int TotalTokens => TokensIn + TokensOut;
}
