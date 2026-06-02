namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Spec 047: test seam for the retry-backoff delay between extraction
/// attempts. Production uses <see cref="TaskDelayProvider.Instance"/>;
/// tests substitute a no-op so retry-path tests don't wait real wall-clock
/// time.
/// </summary>
internal interface IExtractionDelayProvider
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct);
}

internal sealed class TaskDelayProvider : IExtractionDelayProvider
{
    public static readonly TaskDelayProvider Instance = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken ct) =>
        Task.Delay(duration, ct);
}
