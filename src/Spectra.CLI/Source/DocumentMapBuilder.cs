using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Source;

/// <summary>
/// Builds a document map from source documentation files.
/// </summary>
public sealed class DocumentMapBuilder
{
    private readonly SourceDiscovery _discovery;
    private readonly DocumentMapExtractor _extractor;

    public DocumentMapBuilder(SourceConfig? config = null)
    {
        _discovery = new SourceDiscovery(config);
        _extractor = new DocumentMapExtractor();
    }

    /// <summary>
    /// Builds a document map from source files.
    /// </summary>
    public async Task<DocumentMap> BuildAsync(string basePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        var entries = new List<DocumentEntry>();

        foreach (var (absolutePath, relativePath) in _discovery.DiscoverWithRelativePaths(basePath))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(absolutePath, ct);
                var fileInfo = new FileInfo(absolutePath);
                var fileSizeKb = (int)(fileInfo.Length / 1024);

                var entry = _extractor.Extract(content, relativePath, fileSizeKb);
                entries.Add(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Skip files that can't be read
                continue;
            }
        }

        // Sort entries by path for consistent ordering
        entries = entries.OrderBy(e => e.Path).ToList();

        return new DocumentMap
        {
            Documents = entries,
            TotalSizeKb = entries.Sum(e => e.SizeKb)
        };
    }

    /// <summary>
    /// Builds a document map from specific files.
    /// </summary>
    public async Task<DocumentMap> BuildFromFilesAsync(
        IEnumerable<string> absolutePaths,
        string basePath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(absolutePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        var entries = new List<DocumentEntry>();

        foreach (var absolutePath in absolutePaths)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(absolutePath))
            {
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(absolutePath, ct);
                var fileInfo = new FileInfo(absolutePath);
                var fileSizeKb = (int)(fileInfo.Length / 1024);
                var relativePath = Path.GetRelativePath(basePath, absolutePath).Replace('\\', '/');

                var entry = _extractor.Extract(content, relativePath, fileSizeKb);
                entries.Add(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
        }

        return new DocumentMap
        {
            Documents = entries.OrderBy(e => e.Path).ToList(),
            TotalSizeKb = entries.Sum(e => e.SizeKb)
        };
    }

    /// <summary>
    /// Gets a summary of the document map for display.
    /// </summary>
    public static string GetSummary(DocumentMap map)
    {
        var totalFiles = map.Documents.Count;
        var totalSize = map.TotalSizeKb;
        var byType = map.Documents
            .GroupBy(e => Path.GetExtension(e.Path).ToLowerInvariant())
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        return $"{totalFiles} files ({totalSize} KB) - {string.Join(", ", byType)}";
    }
}
