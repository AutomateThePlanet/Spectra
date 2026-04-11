using System.Globalization;

namespace Spectra.CLI.Services;

/// <summary>
/// Formats the <c>RUN TOTAL</c> summary line emitted to
/// <c>.spectra-debug.log</c> after every <c>generate</c> / <c>update</c>
/// run (Spec 040 follow-up). Pure function — keeps
/// <see cref="Spectra.CLI.Infrastructure.DebugLogger"/> dependency-free.
///
/// Example output:
/// <code>
/// RUN TOTAL command=generate suite=checkout calls=24 tokens_in=64480 tokens_out=24240 elapsed=2m45s phases=analysis:1/18.5s,generation:3/52.3s,critic:20/1m34s
/// </code>
///
/// When any phase used the <c>text.Length / 4</c> estimate fallback, the
/// totals gain a <c>~</c> prefix:
/// <code>
/// RUN TOTAL command=generate suite=checkout calls=24 ~tokens_in=64480 ~tokens_out=24240 elapsed=2m45s phases=analysis:1/18.5s,generation:3/52.3s,critic:20/1m34s
/// </code>
/// </summary>
public static class RunSummaryDebugFormatter
{
    /// <summary>
    /// Build the RUN TOTAL line for a completed run. Pulls per-phase +
    /// grand-total counts from <paramref name="tracker"/> and formats
    /// <paramref name="wallClock"/> as the handler-level elapsed time.
    /// </summary>
    public static string FormatRunTotal(
        string command,
        string? suite,
        TokenUsageTracker tracker,
        TimeSpan wallClock,
        RunErrorTracker? errorTracker = null)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        var phases = tracker.GetSummary();
        var total = tracker.GetTotal();
        var anyEstimated = total.Estimated;

        var tokensInField = anyEstimated
            ? $"~tokens_in={total.TokensIn}"
            : $"tokens_in={total.TokensIn}";
        var tokensOutField = anyEstimated
            ? $"~tokens_out={total.TokensOut}"
            : $"tokens_out={total.TokensOut}";

        var phasesField = phases.Count == 0
            ? ""
            : string.Join(",", phases.Select(p =>
                $"{p.Phase}:{p.Calls}/{FormatDuration(p.Elapsed)}"));

        var suiteField = string.IsNullOrEmpty(suite) ? "-" : suite;

        // Spec 043: error + rate-limit suffixes are always present (even on
        // zero) so consumers can grep without conditional parsing.
        var errors = errorTracker?.Errors ?? 0;
        var rateLimits = errorTracker?.RateLimits ?? 0;

        return $"RUN TOTAL command={command} suite={suiteField} calls={total.Calls} "
             + $"{tokensInField} {tokensOutField} elapsed={FormatDuration(wallClock)} "
             + $"phases={phasesField} rate_limits={rateLimits} errors={errors}";
    }

    private static string FormatDuration(TimeSpan ts)
    {
        var seconds = ts.TotalSeconds;
        if (seconds <= 0) return "0s";
        if (seconds < 60) return $"{seconds.ToString("F1", CultureInfo.InvariantCulture)}s";

        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m{ts.Seconds:D2}s";
        }
        return $"{ts.Minutes}m{ts.Seconds:D2}s";
    }
}
