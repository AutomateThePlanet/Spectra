using System.Text.Json;
using Spectra.Core.Models.Index;

namespace Spectra.Core.Index;

/// <summary>
/// Writes <c>_checksums.json</c> (Spec 040 v2 layout) atomically.
/// </summary>
public sealed class ChecksumStoreWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Writes <paramref name="store"/> to <paramref name="path"/> with keys sorted
    /// alphabetically (ordinal) for deterministic git diffs.
    /// </summary>
    public async Task WriteAsync(string path, ChecksumStore store, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(store);

        var content = Render(store);
        await AtomicFileWriter.WriteAllTextAsync(path, content, ct);
    }

    /// <summary>
    /// Renders the store as deterministic indented JSON.
    /// </summary>
    public static string Render(ChecksumStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (store.Version != 2)
        {
            throw new InvalidOperationException(
                $"Checksum store version must be 2 (got {store.Version}).");
        }

        // Sort keys for deterministic output.
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in store.Checksums)
        {
            if (key.Contains('\\'))
            {
                throw new InvalidOperationException(
                    $"Checksum store key '{key}' contains backslash; expected forward slashes only.");
            }
            sorted.Add(key, value);
        }

        var deterministic = new ChecksumStore
        {
            Version = store.Version,
            GeneratedAt = store.GeneratedAt,
            Checksums = new Dictionary<string, string>(sorted, StringComparer.Ordinal),
        };

        return JsonSerializer.Serialize(deterministic, JsonOptions);
    }
}
