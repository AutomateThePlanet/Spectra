using Spectra.Core.Index;

namespace Spectra.Core.IdAllocation;

/// <summary>
/// Cross-process-safe wrapper over the in-memory
/// <see cref="TestIdAllocator"/>. Owns the file lock + high-water-mark file
/// + filesystem frontmatter scan, and guarantees globally unique test IDs
/// even under concurrent generation runs.
/// </summary>
/// <remarks>
/// Spec 040 / Decision 1:
/// <list type="bullet">
///   <item>HWM in <c>.spectra/id-allocator.json</c> (monotonic, never decreases).</item>
///   <item>Lock at <c>.spectra/id-allocator.lock</c> (FileShare.None, 10 s timeout).</item>
///   <item>Effective ID = max(HWM, indexMax, filesystemMax, idStart - 1).</item>
/// </list>
/// </remarks>
public sealed class PersistentTestIdAllocator
{
    private static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(10);

    private readonly string _workspaceRoot;
    private readonly HighWaterMarkStore _hwmStore;
    private readonly string _lockPath;
    private readonly TestCaseFrontmatterScanner _scanner;
    private readonly GlobalIdScanner _indexScanner;
    private readonly Action<string>? _logInfo;

    private bool _seedLogged;

    public PersistentTestIdAllocator(string workspaceRoot, Action<string>? logInfo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = workspaceRoot;
        _hwmStore = new HighWaterMarkStore(Path.Combine(workspaceRoot, ".spectra", "id-allocator.json"));
        _lockPath = Path.Combine(workspaceRoot, ".spectra", "id-allocator.lock");
        _scanner = new TestCaseFrontmatterScanner(Path.Combine(workspaceRoot, "test-cases"));
        _indexScanner = new GlobalIdScanner();
        _logInfo = logInfo;
    }

    /// <summary>
    /// Allocates <paramref name="count"/> globally-unique test IDs. Acquires
    /// the file lock for the duration of the allocation. Updates the HWM
    /// before releasing the lock.
    /// </summary>
    public async Task<IReadOnlyList<string>> AllocateAsync(
        int count,
        string idPrefix,
        int idStart,
        string commandName,
        CancellationToken ct = default)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(idPrefix);

        if (count == 0)
        {
            return Array.Empty<string>();
        }

        using var fileLock = await FileLockHandle.AcquireAsync(_lockPath, DefaultLockTimeout, ct).ConfigureAwait(false);

        var (hwm, warning) = await _hwmStore.ReadWithWarningAsync(ct).ConfigureAwait(false);
        if (warning is not null)
        {
            _logInfo?.Invoke($"[WARN] {warning}");
        }

        var testCasesDir = Path.Combine(_workspaceRoot, "test-cases");
        var indexMax = await _indexScanner.GetMaxIdNumberAsync(testCasesDir, idPrefix, ct).ConfigureAwait(false);
        var filesystemMax = await _scanner.GetMaxIdNumberAsync(idPrefix, ct).ConfigureAwait(false);
        var configFloor = Math.Max(0, idStart - 1);

        var effective = Math.Max(Math.Max(hwm, indexMax), Math.Max(filesystemMax, configFloor));

        // First-run seed log: HWM file was absent and we just rebuilt from
        // filesystem/index. Emit one info line per process.
        if (hwm == 0 && warning is null && !_seedLogged && (indexMax > 0 || filesystemMax > 0))
        {
            _logInfo?.Invoke($"[INFO] Initialized ID allocator: high water mark = {idPrefix}-{effective:D3}");
            _seedLogged = true;
        }

        var ids = Enumerable.Range(effective + 1, count)
            .Select(n => $"{idPrefix}-{n:D3}")
            .ToList();

        var newHwm = effective + count;
        await _hwmStore.WriteAsync(newHwm, commandName, ct).ConfigureAwait(false);

        return ids;
    }

    /// <summary>
    /// Reads the current HWM (without allocating). Used by diagnostics.
    /// </summary>
    public Task<int> PeekHighWaterMarkAsync(CancellationToken ct = default)
        => _hwmStore.ReadAsync(ct);

    /// <summary>
    /// Computes the next ID that would be allocated, without taking the lock
    /// or modifying state. Used by <c>spectra doctor ids</c>.
    /// </summary>
    public async Task<(int Effective, string NextId)> PeekNextAsync(
        string idPrefix,
        int idStart,
        CancellationToken ct = default)
    {
        var hwm = await _hwmStore.ReadAsync(ct).ConfigureAwait(false);
        var testCasesDir = Path.Combine(_workspaceRoot, "test-cases");
        var indexMax = await _indexScanner.GetMaxIdNumberAsync(testCasesDir, idPrefix, ct).ConfigureAwait(false);
        var filesystemMax = await _scanner.GetMaxIdNumberAsync(idPrefix, ct).ConfigureAwait(false);
        var configFloor = Math.Max(0, idStart - 1);
        var effective = Math.Max(Math.Max(hwm, indexMax), Math.Max(filesystemMax, configFloor));
        return (effective, $"{idPrefix}-{effective + 1:D3}");
    }
}
