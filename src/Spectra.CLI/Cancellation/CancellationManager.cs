using System.Diagnostics;

namespace Spectra.CLI.Cancellation;

/// <summary>
/// Spec 040: process-level singleton that owns the
/// <see cref="CancellationTokenSource"/> for the running command, watches
/// the <c>.spectra/.cancel</c> sentinel, and writes/cleans
/// <c>.spectra/.pid</c>. All long-running handlers register here at start
/// and unregister in <c>finally</c>.
/// </summary>
/// <remarks>
/// Decision 5: a SPECTRA process executes exactly one top-level command per
/// invocation, so a singleton is the minimum surface for cross-handler-and-
/// cross-process semantics. Living in <c>Spectra.CLI</c> (not Core) is
/// correct because PID/sentinel coordination is a CLI concern.
/// </remarks>
public sealed class CancellationManager
{
    private static readonly object SyncRoot = new();
    private static CancellationManager? _instance;

    public static CancellationManager Instance
    {
        get
        {
            if (_instance is null)
            {
                lock (SyncRoot)
                {
                    _instance ??= new CancellationManager();
                }
            }
            return _instance;
        }
    }

    /// <summary>For tests: replace the singleton with a manager rooted at a
    /// specific workspace. Caller is responsible for restoring the previous
    /// instance via the returned disposable.</summary>
    public static IDisposable OverrideForTests(string workspaceRoot)
    {
        lock (SyncRoot)
        {
            var previous = _instance;
            _instance = new CancellationManager(workspaceRoot);
            return new Restore(previous);
        }
    }

    private readonly string _workspaceRoot;
    private readonly PidFileManager _pidFile;
    private readonly string _sentinelPath;
    private CancellationTokenSource? _linkedCts;
    private SentinelWatcher? _watcher;
    private string? _activeCommand;

    private CancellationManager() : this(Directory.GetCurrentDirectory()) { }

    private CancellationManager(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
        var spectraDir = Path.Combine(workspaceRoot, ".spectra");
        _pidFile = new PidFileManager(Path.Combine(spectraDir, ".pid"));
        _sentinelPath = Path.Combine(spectraDir, ".cancel");
    }

    public string WorkspaceRoot => _workspaceRoot;
    public string PidPath => _pidFile.Path;
    public string SentinelPath => _sentinelPath;

    /// <summary>
    /// Returns the active token. While unregistered, returns
    /// <see cref="CancellationToken.None"/> — handlers should still pass
    /// <see cref="ThrowIfCancellationRequested"/> defensively.
    /// </summary>
    public CancellationToken Token => _linkedCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> if the linked token is
    /// cancelled OR the <c>.spectra/.cancel</c> sentinel file exists. Used at
    /// batch boundaries inside long-running handlers.
    /// </summary>
    public void ThrowIfCancellationRequested()
    {
        if (_linkedCts?.IsCancellationRequested == true)
        {
            throw new OperationCanceledException(_linkedCts.Token);
        }
        if (File.Exists(_sentinelPath))
        {
            _linkedCts?.Cancel();
            throw new OperationCanceledException(_linkedCts?.Token ?? CancellationToken.None);
        }
    }

    /// <summary>
    /// Registers the running command. Returns a disposable that the caller
    /// MUST dispose (typically via `await using` or in a `finally` block).
    /// Idempotent against double-disposal; throws if a different command is
    /// already registered.
    /// </summary>
    public async Task<Registration> RegisterCommandAsync(string commandName, CancellationToken externalToken = default, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        if (_linkedCts is not null)
        {
            throw new InvalidOperationException(
                $"CancellationManager already registered for command '{_activeCommand}'. Unregister first.");
        }

        // Create the .spectra dir if missing (idempotent).
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, ".spectra"));

        // Defensive: clear any leftover sentinel from a previously crashed run.
        if (File.Exists(_sentinelPath))
        {
            try { File.Delete(_sentinelPath); } catch { /* ignore */ }
        }

        _activeCommand = commandName;
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        var process = Process.GetCurrentProcess();
        await _pidFile.WriteAsync(process.Id, commandName, process.ProcessName, ct).ConfigureAwait(false);

        _watcher = new SentinelWatcher(_sentinelPath);
        _watcher.Start(() =>
        {
            try { _linkedCts?.Cancel(); } catch { /* ignore */ }
        });

        return new Registration(this);
    }

    private void Unregister()
    {
        try { _watcher?.Dispose(); } catch { /* ignore */ }
        _watcher = null;

        try { _linkedCts?.Cancel(); } catch { /* ignore */ }
        try { _linkedCts?.Dispose(); } catch { /* ignore */ }
        _linkedCts = null;

        _pidFile.Delete();
        try
        {
            if (File.Exists(_sentinelPath))
            {
                File.Delete(_sentinelPath);
            }
        }
        catch { /* ignore */ }

        _activeCommand = null;
    }

    public sealed class Registration : IDisposable
    {
        private readonly CancellationManager _manager;
        private bool _disposed;

        internal Registration(CancellationManager manager)
        {
            _manager = manager;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _manager.Unregister();
        }
    }

    private sealed class Restore : IDisposable
    {
        private readonly CancellationManager? _previous;
        private bool _disposed;
        public Restore(CancellationManager? previous) { _previous = previous; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (SyncRoot)
            {
                _instance = _previous;
            }
        }
    }
}
