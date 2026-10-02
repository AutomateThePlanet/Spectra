using System.Text;
using Spectra.Core.Baseline;
using Spectra.Core.IdAllocation;

namespace Spectra.CLI.Baseline;

/// <summary>
/// Spec 084 (FR-007–FR-009, research R6): the single owner of baseline and snapshot I/O. Writers run inside
/// <see cref="WithLockAsync{T}"/> on <c>.spectra/baseline.lock</c>, load suites strictly (an unreadable baseline is
/// <see cref="ErrorBaselineUnreadable"/>, never treated as empty), and save atomically, skipping byte-identical
/// content. Snapshots are content-addressed by fingerprint and never rewritten or pruned.
/// </summary>
public static class BaselineStore
{
    public const string ErrorLockTimeout = "LOCK_TIMEOUT";
    public const string ErrorBaselineUnreadable = "BASELINE_UNREADABLE";

    /// <summary>An ingest persisted its tests but writing their baseline or snapshots failed (exit 1, with the IDs).</summary>
    public const string ErrorBaselineWriteFailed = "BASELINE_WRITE_FAILED";

    public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Tests only: a shorter lock timeout.</summary>
    internal static TimeSpan? LockTimeoutOverride { get; set; }

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string LockPath(string repoRoot) => Path.Combine(repoRoot, ".spectra", "baseline.lock");

    public static string SnapshotDir(string repoRoot) => Path.Combine(repoRoot, ".spectra", "source-snapshots");

    public static string SuiteFile(string testsPath, string suite) =>
        Path.Combine(testsPath, suite, SourceBaseline.FileName);

    public static async Task<T> WithLockAsync<T>(string repoRoot, Func<Task<T>> body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var lockPath = LockPath(repoRoot);

        FileLockHandle handle;
        try
        {
            handle = await FileLockHandle.AcquireAsync(lockPath, LockTimeoutOverride ?? DefaultLockTimeout, ct);
        }
        catch (TimeoutException)
        {
            throw new BaselineStoreException(ErrorLockTimeout,
                $"Could not acquire the baseline lock ({Relative(repoRoot, lockPath)}); another SPECTRA command is writing baselines.");
        }

        using (handle)
        {
            return await body();
        }
    }

    /// <summary>Loads a suite's baseline. A missing file is an empty baseline; an unreadable one throws.</summary>
    public static async Task<SourceBaseline> LoadSuiteAsync(string repoRoot, string testsPath, string suite, CancellationToken ct)
    {
        var path = SuiteFile(testsPath, suite);
        if (!File.Exists(path))
        {
            return new SourceBaseline();
        }

        var relative = Relative(repoRoot, path);
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BaselineStoreException(ErrorBaselineUnreadable, $"{relative} could not be read: {ex.Message}");
        }

        try
        {
            return SourceBaselineSerializer.Parse(text, relative);
        }
        catch (BaselineFormatException ex)
        {
            throw new BaselineStoreException(ErrorBaselineUnreadable, ex.Message);
        }
    }

    /// <summary>
    /// Saves a suite's baseline, first dropping entries for tests not in <paramref name="liveIds"/>. Returns false when
    /// nothing was written (identical content, or an empty baseline and no file yet).
    /// </summary>
    public static async Task<bool> SaveSuiteAsync(
        string testsPath, string suite, SourceBaseline baseline, IReadOnlySet<string> liveIds, CancellationToken ct)
    {
        foreach (var stale in baseline.Tests.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            baseline.Tests.Remove(stale);
        }

        var path = SuiteFile(testsPath, suite);
        var exists = File.Exists(path);
        if (!exists && baseline.Tests.Count == 0)
        {
            return false;
        }

        var text = SourceBaselineSerializer.Serialize(baseline);
        if (exists && string.Equals(await File.ReadAllTextAsync(path, ct), text, StringComparison.Ordinal))
        {
            return false;
        }

        await WriteAtomicAsync(path, text, ct);
        return true;
    }

    /// <summary>Writes the snapshot for <paramref name="fingerprint"/> if it doesn't exist yet. Returns true if written.</summary>
    public static async Task<bool> EnsureSnapshotAsync(string repoRoot, string fingerprint, string normalised, CancellationToken ct)
    {
        var path = SnapshotPath(repoRoot, fingerprint);
        if (File.Exists(path))
        {
            return false;
        }

        try
        {
            await WriteAtomicAsync(path, normalised, ct, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false; // created concurrently — same content by construction
        }
    }

    public static async Task<string?> ReadSnapshotAsync(string repoRoot, string fingerprint, CancellationToken ct)
    {
        var path = SnapshotPath(repoRoot, fingerprint);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, Utf8NoBom, ct) : null;
    }

    /// <summary>Writes a scratch or data file atomically (temp file in the same folder, then move).</summary>
    public static async Task WriteAtomicAsync(string path, string text, CancellationToken ct, bool overwrite = true)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temp, text, Utf8NoBom, ct);
            File.Move(temp, path, overwrite);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static string SnapshotPath(string repoRoot, string fingerprint) =>
        Path.Combine(SnapshotDir(repoRoot), SourceFingerprint.Hex(fingerprint) + ".txt");

    internal static string Relative(string repoRoot, string path) =>
        Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
}

/// <summary>A baseline operation failed with a machine-readable error code.</summary>
public sealed class BaselineStoreException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
