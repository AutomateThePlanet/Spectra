using Spectra.CLI.Agent.Copilot;

namespace Spectra.Integration.Tests.Support;

/// <summary>
/// Spec 052: records each requested retry backoff and returns immediately, so
/// the 047 retry path can be exercised without real wall-clock delay.
/// </summary>
internal sealed class NoOpDelayProvider : IExtractionDelayProvider
{
    public readonly List<TimeSpan> Calls = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken ct)
    {
        Calls.Add(duration);
        return Task.CompletedTask;
    }
}
