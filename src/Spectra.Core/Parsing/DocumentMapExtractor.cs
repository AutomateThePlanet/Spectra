using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Spectra.Core.Models;

namespace Spectra.Core.Parsing;

/// <summary>
/// Extracts metadata from documentation files for the document map.
/// </summary>
public sealed partial class DocumentMapExtractor
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .Build();

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    internal static partial Regex TitleRegex();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    internal static partial Regex H2Regex();

    [GeneratedRegex(@"^###\s+(.+)$", RegexOptions.Multiline)]
    internal static partial Regex H3Regex();

    /// <summary>
    /// Extracts a DocumentEntry from a Markdown file.
    /// </summary>
    /// <param name="content">The Markdown content</param>
    /// <param name="relativePath">Relative path from docs/</param>
    /// <param name="fileSizeKb">File size in KB</param>
    /// <returns>DocumentEntry with extracted metadata</returns>
    public DocumentEntry Extract(string content, string relativePath, int fileSizeKb)
    {
        var title = ExtractTitle(content) ?? Path.GetFileNameWithoutExtension(relativePath);
        var headings = ExtractHeadings(content);
        var preview = ExtractPreview(content);

        return new DocumentEntry
        {
            Path = relativePath,
            Title = title,
            SizeKb = fileSizeKb,
            Headings = headings,
            Preview = preview
        };
    }

    /// <summary>
    /// Extracts a DocumentEntry from a file.
    /// </summary>
    public async Task<DocumentEntry> ExtractFromFileAsync(string absolutePath, string relativePath, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(absolutePath, ct);
        var fileInfo = new FileInfo(absolutePath);
        var fileSizeKb = (int)Math.Ceiling(fileInfo.Length / 1024.0);

        return Extract(content, relativePath, fileSizeKb);
    }

    private static string? ExtractTitle(string content)
    {
        var match = TitleRegex().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static IReadOnlyList<string> ExtractHeadings(string content)
    {
        var headings = new List<string>();

        // Get H1
        var h1Match = TitleRegex().Match(content);
        if (h1Match.Success)
        {
            headings.Add(h1Match.Groups[1].Value.Trim());
        }

        // Get H2s
        var h2Matches = H2Regex().Matches(content);
        foreach (Match match in h2Matches)
        {
            headings.Add(match.Groups[1].Value.Trim());
        }

        return headings;
    }

    private static string ExtractPreview(string content)
    {
        // Skip frontmatter if present
        var bodyStart = 0;
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
            if (endIndex > 0)
            {
                bodyStart = endIndex + 3;
            }
        }

        // Skip title heading
        var body = content[bodyStart..].Trim();
        var titleMatch = TitleRegex().Match(body);
        if (titleMatch.Success)
        {
            body = body[(titleMatch.Index + titleMatch.Length)..].Trim();
        }

        // Extract first 200 characters of meaningful content
        var preview = Regex.Replace(body, @"^#+\s+.*$", "", RegexOptions.Multiline);
        preview = Regex.Replace(preview, @"\s+", " ").Trim();

        return preview.Length <= 200 ? preview : preview[..200] + "...";
    }
}
