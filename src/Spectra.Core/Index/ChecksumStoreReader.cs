using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Models.Index;

namespace Spectra.Core.Index;

/// <summary>
/// Reads <c>_checksums.json</c> (Spec 040 v2 layout). Used for incremental
/// update detection. <strong>Must never feed into AI prompts.</strong>
/// </summary>
public sealed partial class ChecksumStoreReader
{
    [GeneratedRegex(@"^[a-f0-9]{64}$")]
    private static partial Regex HexDigestRegex();

    /// <summary>
    /// Reads the store at <paramref name="path"/>. Returns null if absent.
    /// Throws on malformed digests or backslash-containing keys (programming bug).
    /// </summary>
    public async Task<ChecksumStore?> ReadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return Parse(json);
    }

    /// <summary>
    /// Parses a JSON string into a <see cref="ChecksumStore"/>.
    /// </summary>
    public static ChecksumStore Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        ChecksumStore? store;
        try
        {
            store = JsonSerializer.Deserialize<ChecksumStore>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse checksum store JSON: {ex.Message}", ex);
        }

        if (store is null)
        {
            throw new InvalidOperationException("Checksum store JSON deserialized to null.");
        }

        Validate(store);
        return store;
    }

    private static void Validate(ChecksumStore store)
    {
        if (store.Version != 2)
        {
            throw new InvalidOperationException(
                $"Unsupported checksum store version: {store.Version}. Expected 2.");
        }

        store.Checksums ??= new Dictionary<string, string>();

        foreach (var (key, value) in store.Checksums)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Checksum store contains empty key.");
            }
            if (key.Contains('\\'))
            {
                throw new InvalidOperationException(
                    $"Checksum store key '{key}' contains backslash; expected forward slashes only.");
            }
            if (!HexDigestRegex().IsMatch(value))
            {
                throw new InvalidOperationException(
                    $"Checksum for '{key}' is not a 64-character lowercase hex digest: '{value}'.");
            }
        }
    }
}
