using System.Threading;

namespace Spectra.CLI.Services;

/// <summary>
/// Spec 043: per-run counters for error visibility in the run summary.
/// Tracks total errors and rate-limit incidents across all phases. Both
/// counters are thread-safe (parallel critic tasks may bump them
/// concurrently).
/// </summary>
public sealed class RunErrorTracker
{
    private int _errors;
    private int _rateLimits;

    public int Errors => Volatile.Read(ref _errors);

    public int RateLimits => Volatile.Read(ref _rateLimits);

    /// <summary>
    /// Record any caught exception. If <paramref name="ex"/> is classified
    /// as a rate-limit failure (HTTP 429 or recognizable message), the
    /// rate-limit counter is bumped in addition to the error counter.
    /// </summary>
    public void Record(Exception ex)
    {
        Interlocked.Increment(ref _errors);
        if (Spectra.CLI.Infrastructure.ErrorLogger.IsRateLimit(ex))
        {
            Interlocked.Increment(ref _rateLimits);
        }
    }

    /// <summary>
    /// Record a non-exception error (e.g., a synthesized failure with no
    /// underlying exception object). Bumps only the error counter.
    /// </summary>
    public void RecordError() => Interlocked.Increment(ref _errors);
}
