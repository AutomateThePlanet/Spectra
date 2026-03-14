using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;

namespace Spectra.Core.Index;

/// <summary>
/// Writes and reads metadata index files.
/// </summary>
public sealed class IndexWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Writes the index to a file.
    /// </summary>
    public async Task WriteAsync(string path, MetadataIndex index, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(index);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(index, WriteOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    /// <summary>
    /// Reads the index from a file.
    /// </summary>
    public async Task<MetadataIndex?> ReadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<MetadataIndex>(json, ReadOptions);
    }

    /// <summary>
    /// Checks if an index file exists.
    /// </summary>
    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    /// <summary>
    /// Gets the standard index file path for a suite.
    /// </summary>
    public static string GetIndexPath(string suitePath)
    {
        return Path.Combine(suitePath, "_index.json");
    }
}
