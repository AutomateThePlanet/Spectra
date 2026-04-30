namespace Spectra.CLI.Cancellation;

/// <summary>
/// Spec 040: polls <c>.spectra/.cancel</c> at a fixed interval and triggers
/// a cancellation callback when the sentinel file appears. Used by
/// <see cref="CancellationManager"/> as the bridge from the peer
/// <c>spectra cancel</c> process into the running command's
/// <see cref="CancellationTokenSource"/>.
/// </summary>
/// <remarks>
/// Decision 4: 200 ms polling instead of FileSystemWatcher — overkill for a
/// file checked at most a few times per second, and watchers have known
/// cross-platform quirks with rapidly-created-then-deleted files.
/// </remarks>
public sealed class SentinelWatcher : IDisposable
{
    private readonly string _sentinelPath;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _internalCts = new();
    private Task? _watchTask;
    private Action? _onTriggered;

    public SentinelWatcher(string sentinelPath, TimeSpan? pollInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sentinelPath);
        _sentinelPath = sentinelPath;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
    }

    /// <summary>
    /// Starts polling. <paramref name="onTriggered"/> is invoked on a thread
    /// pool thread when the sentinel file is detected. Subsequent detections
    /// are suppressed (the watcher fires once).
    /// </summary>
    public void Start(Action onTriggered)
    {
        ArgumentNullException.ThrowIfNull(onTriggered);
        if (_watchTask is not null)
        {
            throw new InvalidOperationException("Watcher already started");
        }
        _onTriggered = onTriggered;
        _watchTask = Task.Run(WatchAsync);
    }

    private async Task WatchAsync()
    {
        var triggered = false;
        try
        {
            while (!_internalCts.IsCancellationRequested)
            {
                if (!triggered && File.Exists(_sentinelPath))
                {
                    triggered = true;
                    try
                    {
                        _onTriggered?.Invoke();
                    }
                    catch
                    {
                        // suppress callback exceptions
                    }
                    return;
                }
                await Task.Delay(_pollInterval, _internalCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public void Dispose()
    {
        try
        {
            _internalCts.Cancel();
            _watchTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignore
        }
        finally
        {
            _internalCts.Dispose();
        }
    }
}
