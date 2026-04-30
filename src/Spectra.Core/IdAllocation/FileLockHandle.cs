namespace Spectra.Core.IdAllocation;

/// <summary>
/// Cross-process exclusive file lock. Acquired by opening
/// <paramref name="lockPath"/> with <see cref="FileShare.None"/>; the OS
/// releases the handle on process exit (including crash). Disposing the
/// handle releases the lock and deletes the lock file (best-effort).
/// </summary>
/// <remarks>
/// Spec 040 / Decision 2: retry-with-exponential-backoff up to a 10-second
/// total timeout, then throw <see cref="TimeoutException"/>.
/// </remarks>
public sealed class FileLockHandle : IDisposable
{
    private readonly string _path;
    private FileStream? _stream;

    private FileLockHandle(string path, FileStream stream)
    {
        _path = path;
        _stream = stream;
    }

    public static async Task<FileLockHandle> AcquireAsync(
        string lockPath,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockPath);

        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        var delayMs = 50;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.None);
                return new FileLockHandle(lockPath, stream);
            }
            catch (IOException)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        $"Failed to acquire lock on '{lockPath}' within {timeout.TotalSeconds:F1}s.");
                }

                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                delayMs = Math.Min(delayMs * 2, 1000);
            }
        }
    }

    public void Dispose()
    {
        if (_stream is not null)
        {
            try
            {
                _stream.Dispose();
            }
            catch
            {
                // ignore
            }
            _stream = null;

            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch
            {
                // best-effort; another process may already hold the lock again
            }
        }
    }
}
