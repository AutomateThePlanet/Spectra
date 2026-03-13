using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.CLI.IO;

/// <summary>
/// Manages suite-level lock files for concurrent access prevention.
/// </summary>
public sealed class LockManager
{
    private const string LockFileName = ".spectra.lock";
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(10);

    private readonly TimeSpan _lockExpiry;

    public LockManager(TimeSpan? lockExpiry = null)
    {
        _lockExpiry = lockExpiry ?? DefaultExpiry;
    }

    /// <summary>
    /// Attempts to acquire a lock for the specified suite.
    /// </summary>
    /// <returns>A disposable lock handle, or null if lock could not be acquired.</returns>
    public async Task<LockHandle?> TryAcquireAsync(
        string suitePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suitePath);

        var lockPath = GetLockPath(suitePath);

        // Check for existing lock
        if (File.Exists(lockPath))
        {
            var existingLock = await ReadLockInfoAsync(lockPath, ct);

            if (existingLock is not null)
            {
                // Check if lock is expired
                var lockAge = DateTime.UtcNow - existingLock.StartedAt;
                if (lockAge < _lockExpiry)
                {
                    // Lock is still valid
                    return null;
                }

                // Lock is expired, remove it
                TryDeleteLock(lockPath);
            }
        }

        // Create lock file
        var lockInfo = new LockInfo
        {
            ProcessId = Environment.ProcessId,
            StartedAt = DateTime.UtcNow,
            MachineName = Environment.MachineName
        };

        try
        {
            await WriteLockInfoAsync(lockPath, lockInfo, ct);
            return new LockHandle(lockPath, this);
        }
        catch (IOException)
        {
            // Another process may have created the lock
            return null;
        }
    }

    /// <summary>
    /// Acquires a lock, waiting if necessary.
    /// </summary>
    /// <exception cref="SuiteLockedException">If lock cannot be acquired.</exception>
    public async Task<LockHandle> AcquireAsync(
        string suitePath,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var handle = await TryAcquireAsync(suitePath, ct);
            if (handle is not null)
            {
                return handle;
            }

            // Wait before retrying
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        // Read lock info for error message
        var lockPath = GetLockPath(suitePath);
        var lockInfo = await ReadLockInfoAsync(lockPath, ct);

        throw new SuiteLockedException(suitePath, lockInfo?.ProcessId, lockInfo?.MachineName);
    }

    /// <summary>
    /// Releases a lock.
    /// </summary>
    public void Release(string lockPath)
    {
        TryDeleteLock(lockPath);
    }

    /// <summary>
    /// Checks if a suite is currently locked.
    /// </summary>
    public async Task<bool> IsLockedAsync(string suitePath, CancellationToken ct = default)
    {
        var lockPath = GetLockPath(suitePath);

        if (!File.Exists(lockPath))
        {
            return false;
        }

        var lockInfo = await ReadLockInfoAsync(lockPath, ct);
        if (lockInfo is null)
        {
            return false;
        }

        var lockAge = DateTime.UtcNow - lockInfo.StartedAt;
        return lockAge < _lockExpiry;
    }

    /// <summary>
    /// Gets information about an existing lock.
    /// </summary>
    public async Task<LockInfo?> GetLockInfoAsync(string suitePath, CancellationToken ct = default)
    {
        var lockPath = GetLockPath(suitePath);
        return await ReadLockInfoAsync(lockPath, ct);
    }

    /// <summary>
    /// Forces removal of a lock (use with caution).
    /// </summary>
    public bool ForceRemove(string suitePath)
    {
        var lockPath = GetLockPath(suitePath);
        return TryDeleteLock(lockPath);
    }

    private static string GetLockPath(string suitePath)
    {
        return Path.Combine(suitePath, LockFileName);
    }

    private static async Task<LockInfo?> ReadLockInfoAsync(string lockPath, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(lockPath, ct);
            return JsonSerializer.Deserialize<LockInfo>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static async Task WriteLockInfoAsync(string lockPath, LockInfo info, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Use FileMode.CreateNew to fail if file already exists
        await using var fs = new FileStream(
            lockPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await using var writer = new StreamWriter(fs);
        await writer.WriteAsync(json);
    }

    private static bool TryDeleteLock(string lockPath)
    {
        try
        {
            File.Delete(lockPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>
/// Information stored in a lock file.
/// </summary>
public sealed class LockInfo
{
    [JsonPropertyName("pid")]
    public int ProcessId { get; init; }

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; init; }

    [JsonPropertyName("machine")]
    public string? MachineName { get; init; }
}

/// <summary>
/// Handle representing an acquired lock.
/// </summary>
public sealed class LockHandle : IDisposable
{
    private readonly string _lockPath;
    private readonly LockManager _manager;
    private bool _disposed;

    internal LockHandle(string lockPath, LockManager manager)
    {
        _lockPath = lockPath;
        _manager = manager;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _manager.Release(_lockPath);
        _disposed = true;
    }
}

/// <summary>
/// Exception thrown when a suite is locked by another process.
/// </summary>
public sealed class SuiteLockedException : Exception
{
    public string SuitePath { get; }
    public int? LockingProcessId { get; }
    public string? LockingMachine { get; }

    public SuiteLockedException(string suitePath, int? processId, string? machineName)
        : base($"Suite '{suitePath}' is locked by process {processId}{(machineName is not null ? $" on {machineName}" : "")}")
    {
        SuitePath = suitePath;
        LockingProcessId = processId;
        LockingMachine = machineName;
    }
}
