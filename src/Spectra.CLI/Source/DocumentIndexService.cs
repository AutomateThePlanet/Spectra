using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Source;

/// <summary>
/// Service for building and maintaining the document index.
/// </summary>
public sealed class DocumentIndexService
{
    private readonly DocumentIndexExtractor _extractor = new();
    private readonly DocumentIndexWriter _writer = new();
    private readonly DocumentIndexReader _reader = new();

    /// <summary>
    /// Ensures the document index is up-to-date, performing incremental updates as needed.
    /// </summary>
    public async Task<DocumentIndex> EnsureIndexAsync(
        string basePath,
        SourceConfig config,
        bool forceRebuild = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(config);

        var indexPath = ResolveIndexPath(basePath, config);
        var files = DiscoverFiles(basePath, config, indexPath);

        if (files.Count == 0)
        {
            var emptyIndex = new DocumentIndex
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                TotalWordCount = 0,
                TotalEstimatedTokens = 0,
                Entries = []
            };
            await _writer.WriteAsync(indexPath, emptyIndex, ct);
            return emptyIndex;
        }

        // Read existing checksums for incremental mode
        Dictionary<string, string>? storedHashes = null;
        if (!forceRebuild && File.Exists(indexPath))
        {
            storedHashes = await _reader.ReadHashesOnlyAsync(indexPath, ct);
        }

        // Compute current hashes and classify files
        var currentHashes = new Dictionary<string, string>();
        foreach (var (absolutePath, _) in files)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(absolutePath, ct);
            currentHashes[absolutePath] = DocumentIndexExtractor.ComputeHash(content);
        }

        var unchanged = new List<(string AbsPath, string RelPath)>();
        var changed = new List<(string AbsPath, string RelPath)>();

        foreach (var (absolutePath, relativePath) in files)
        {
            if (storedHashes is not null
                && storedHashes.TryGetValue(relativePath, out var storedHash)
                && currentHashes[absolutePath] == storedHash)
            {
                unchanged.Add((absolutePath, relativePath));
            }
            else
            {
                changed.Add((absolutePath, relativePath));
            }
        }

        // Reuse unchanged entries from existing index if possible
        var unchangedPaths = new HashSet<string>(unchanged.Select(u => u.RelPath));
        var reusedEntries = new Dictionary<string, DocumentIndexEntry>();
        if (unchanged.Count > 0 && !forceRebuild)
        {
            var existingIndex = await _reader.ReadFullAsync(indexPath, ct);
            if (existingIndex is not null)
            {
                foreach (var entry in existingIndex.Entries)
                {
                    if (unchangedPaths.Contains(entry.Path))
                    {
                        reusedEntries[entry.Path] = entry;
                    }
                }
            }
        }

        // Extract metadata for changed/new files
        var entries = new List<DocumentIndexEntry>();
        foreach (var (absolutePath, relativePath) in files)
        {
            ct.ThrowIfCancellationRequested();

            if (reusedEntries.TryGetValue(relativePath, out var existing))
            {
                entries.Add(existing);
            }
            else
            {
                var entry = await _extractor.ExtractFromFileAsync(absolutePath, relativePath, ct);
                entries.Add(entry);
            }
        }

        // Sort by path for consistent ordering
        entries.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

        var index = new DocumentIndex
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalWordCount = entries.Sum(e => e.WordCount),
            TotalEstimatedTokens = entries.Sum(e => e.EstimatedTokens),
            Entries = entries
        };

        await _writer.WriteAsync(indexPath, index, ct);
        return index;
    }

    /// <summary>
    /// Gets the number of changed files from the last EnsureIndexAsync call.
    /// Returns (total, changed) counts useful for reporting.
    /// </summary>
    public async Task<(int Total, int Changed)> GetUpdateStatsAsync(
        string basePath,
        SourceConfig config,
        CancellationToken ct = default)
    {
        var indexPath = ResolveIndexPath(basePath, config);
        var files = DiscoverFiles(basePath, config, indexPath);

        if (!File.Exists(indexPath))
        {
            return (files.Count, files.Count);
        }

        var storedHashes = await _reader.ReadHashesOnlyAsync(indexPath, ct);
        if (storedHashes is null)
        {
            return (files.Count, files.Count);
        }

        var changedCount = 0;
        foreach (var (absolutePath, relativePath) in files)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(absolutePath, ct);
            var hash = DocumentIndexExtractor.ComputeHash(content);

            if (!storedHashes.TryGetValue(relativePath, out var storedHash) || hash != storedHash)
            {
                changedCount++;
            }
        }

        return (files.Count, changedCount);
    }

    /// <summary>
    /// Projects a DocumentIndex to a DocumentMap for backward compatibility.
    /// </summary>
    public static DocumentMap ToDocumentMap(DocumentIndex index)
    {
        var entries = index.Entries.Select(e => new DocumentEntry
        {
            Path = e.Path,
            Title = e.Title,
            SizeKb = e.SizeKb,
            Headings = e.Sections.Select(s => s.Heading).ToList(),
            Preview = e.Sections.Count > 0
                ? e.Sections[0].Summary
                : ""
        }).ToList();

        return new DocumentMap
        {
            TotalSizeKb = entries.Sum(e => e.SizeKb),
            Documents = entries
        };
    }

    /// <summary>
    /// Resolves the index file path from config.
    /// </summary>
    public static string ResolveIndexPath(string basePath, SourceConfig config)
    {
        if (!string.IsNullOrEmpty(config.DocIndex))
        {
            return Path.IsPathRooted(config.DocIndex)
                ? config.DocIndex
                : Path.Combine(basePath, config.DocIndex);
        }

        return Path.Combine(basePath, config.LocalDir.TrimEnd('/', '\\'), "_index.md");
    }

    private static List<(string AbsolutePath, string RelativePath)> DiscoverFiles(
        string basePath, SourceConfig config, string indexPath)
    {
        var indexRelative = Path.GetRelativePath(basePath, indexPath).Replace('\\', '/');
        var discovery = new SourceDiscovery(config);
        return discovery.DiscoverWithRelativePaths(basePath)
            .Where(f => !string.Equals(f.RelativePath, indexRelative, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
