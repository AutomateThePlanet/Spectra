using System.Text.RegularExpressions;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Source;

/// <summary>
/// Loads source documents with full content for AI-powered test generation.
/// </summary>
public sealed partial class SourceDocumentLoader
{
    private readonly SourceDiscovery _discovery;

    [GeneratedRegex(@"^#+\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    public SourceDocumentLoader(SourceConfig? config = null)
    {
        _discovery = new SourceDiscovery(config);
    }

    /// <summary>
    /// Loads all source documents with full content.
    /// </summary>
    public async Task<IReadOnlyList<SourceDocument>> LoadAllAsync(
        string basePath,
        int? maxDocuments = null,
        int? maxContentLengthPerDoc = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        var documents = new List<SourceDocument>();
        var filePaths = _discovery.DiscoverWithRelativePaths(basePath).ToList();

        if (maxDocuments.HasValue)
        {
            filePaths = filePaths.Take(maxDocuments.Value).ToList();
        }

        foreach (var (absolutePath, relativePath) in filePaths)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var doc = await LoadSingleAsync(absolutePath, relativePath, maxContentLengthPerDoc, ct);
                if (doc is not null)
                {
                    documents.Add(doc);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Skip files that can't be read
                continue;
            }
        }

        return documents.OrderBy(d => d.Path).ToList();
    }

    /// <summary>
    /// Loads a single source document.
    /// </summary>
    public async Task<SourceDocument?> LoadSingleAsync(
        string absolutePath,
        string relativePath,
        int? maxContentLength = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(absolutePath, ct);
        var fileInfo = new FileInfo(absolutePath);
        var sizeKb = (int)(fileInfo.Length / 1024);

        // Truncate content if needed
        if (maxContentLength.HasValue && content.Length > maxContentLength.Value)
        {
            content = content[..maxContentLength.Value] + "\n\n[Content truncated...]";
        }

        var title = ExtractTitle(content) ?? Path.GetFileNameWithoutExtension(relativePath);
        var sections = ExtractSections(content);

        return new SourceDocument
        {
            Path = relativePath,
            Title = title,
            Content = content,
            Sections = sections,
            SizeKb = sizeKb
        };
    }

    /// <summary>
    /// Extracts the document title from the first H1 heading.
    /// </summary>
    private static string? ExtractTitle(string content)
    {
        var match = TitleRegex().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts all section headings from the document.
    /// </summary>
    private static IReadOnlyList<string> ExtractSections(string content)
    {
        var sections = new List<string>();
        var matches = HeadingRegex().Matches(content);

        foreach (Match match in matches)
        {
            var heading = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(heading))
            {
                sections.Add(heading);
            }
        }

        return sections;
    }
}
