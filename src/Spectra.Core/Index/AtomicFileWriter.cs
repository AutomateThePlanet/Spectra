namespace Spectra.Core.Index;

/// <summary>
/// Write-to-temp-then-rename helper. Used by the v2 documentation-index
/// writers (manifest, checksum store, per-suite files) so a partial write
/// never leaves a half-rendered file in place. Atomic on the same volume on
/// both Windows and POSIX in .NET 8.
/// </summary>
public static class AtomicFileWriter
{
    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically:
    /// content goes to <c>{path}.tmp</c> first, then the tmp file is moved over
    /// the destination via <see cref="File.Move(string, string, bool)"/>.
    /// Creates the parent directory if missing.
    /// </summary>
    public static async Task WriteAllTextAsync(
        string path,
        string content,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup: leave the destination as it was, drop the temp.
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Suppress — the original failure is what matters to the caller.
            }
            throw;
        }
    }

    /// <summary>
    /// Byte-array overload. Same atomic semantics as the string overload.
    /// </summary>
    public static async Task WriteAllBytesAsync(
        string path,
        byte[] content,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";

        try
        {
            await File.WriteAllBytesAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Suppress.
            }
            throw;
        }
    }
}
