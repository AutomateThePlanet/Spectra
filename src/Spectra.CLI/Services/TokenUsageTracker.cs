namespace Spectra.CLI.Services;

/// <summary>
/// Thread-safe collector for per-AI-call token usage (Spec 040).
/// One instance per command run. Handlers create it, pass it to agents,
/// agents call <see cref="Record"/> after every successful AI response,
/// and the handler calls <see cref="GetSummary"/> / <see cref="GetTotal"/>
/// when building the final Run Summary.
///
/// When the API response does not include a <c>usage</c> object, callers
/// pass <c>null</c> for <paramref name="tokensIn"/>/<paramref name="tokensOut"/>;
/// the call is still counted and the elapsed time is still recorded, but
/// the token counters contribute <c>0</c> to totals.
/// </summary>
public sealed class TokenUsageTracker
{
    private readonly List<PhaseUsage> _entries = new();
    private readonly object _lock = new();

    /// <summary>
    /// Record a single AI call. Safe to call from multiple threads.
    /// <paramref name="estimated"/> is true when <paramref name="tokensIn"/>
    /// and <paramref name="tokensOut"/> came from
    /// <see cref="TokenEstimator"/> fallback (text.Length / 4) instead of
    /// provider-reported <c>AssistantUsageEvent</c> counts.
    /// </summary>
    public void Record(
        string phase,
        string model,
        string provider,
        int? tokensIn,
        int? tokensOut,
        TimeSpan elapsed,
        bool estimated = false)
    {
        var entry = new PhaseUsage(
            phase ?? "",
            model ?? "",
            provider ?? "",
            1,
            tokensIn ?? 0,
            tokensOut ?? 0,
            elapsed,
            estimated);

        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>
    /// Aggregate entries by (phase, model, provider). Sums calls, tokens,
    /// and elapsed.
    /// </summary>
    public IReadOnlyList<PhaseUsage> GetSummary()
    {
        PhaseUsage[] snapshot;
        lock (_lock)
        {
            snapshot = _entries.ToArray();
        }

        var result = new List<PhaseUsage>();
        foreach (var group in snapshot.GroupBy(e => (e.Phase, e.Model, e.Provider)))
        {
            var calls = 0;
            var tokensIn = 0;
            var tokensOut = 0;
            var elapsed = TimeSpan.Zero;
            var estimated = false;
            foreach (var e in group)
            {
                calls += e.Calls;
                tokensIn += e.TokensIn;
                tokensOut += e.TokensOut;
                elapsed += e.Elapsed;
                estimated |= e.Estimated;
            }

            result.Add(new PhaseUsage(
                group.Key.Phase,
                group.Key.Model,
                group.Key.Provider,
                calls,
                tokensIn,
                tokensOut,
                elapsed,
                estimated));
        }

        return result
            .OrderBy(p => PhaseOrder(p.Phase))
            .ThenBy(p => p.Model)
            .ToList();
    }

    /// <summary>
    /// Grand total across all entries.
    /// </summary>
    public PhaseUsage GetTotal()
    {
        PhaseUsage[] snapshot;
        lock (_lock)
        {
            snapshot = _entries.ToArray();
        }

        var calls = 0;
        var tokensIn = 0;
        var tokensOut = 0;
        var elapsed = TimeSpan.Zero;
        var estimated = false;
        foreach (var e in snapshot)
        {
            calls += e.Calls;
            tokensIn += e.TokensIn;
            tokensOut += e.TokensOut;
            elapsed += e.Elapsed;
            estimated |= e.Estimated;
        }

        return new PhaseUsage("TOTAL", "", "", calls, tokensIn, tokensOut, elapsed, estimated);
    }

    public bool HasData()
    {
        lock (_lock)
        {
            return _entries.Count > 0;
        }
    }

    private static int PhaseOrder(string phase) => phase switch
    {
        "analysis" => 0,
        "generation" => 1,
        "critic" => 2,
        "update" => 3,
        "criteria" => 4,
        _ => 99
    };
}
