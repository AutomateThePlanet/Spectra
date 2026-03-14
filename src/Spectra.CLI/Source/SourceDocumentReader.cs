using Spectra.Core.Models.Config;

namespace Spectra.CLI.Source;

/// <summary>
/// Reads source document content with size limits and section support.
/// </summary>
public sealed class SourceDocumentReader
{
    private readonly int _maxFileSizeKb;

    public SourceDocumentReader(SourceConfig? config = null)
    {
        _maxFileSizeKb = config?.MaxFileSizeKb ?? 50;
    }

    /// <summary>
    /// Reads the full content of a document, truncated to max size.
    /// </summary>
    public async Task<DocumentContent> ReadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return new DocumentContent
            {
                FilePath = filePath,
                Content = string.Empty,
                IsTruncated = false,
                OriginalSizeBytes = 0,
                LoadedSizeBytes = 0
            };
        }

        var fileInfo = new FileInfo(filePath);
        var originalSize = fileInfo.Length;
        var maxBytes = _maxFileSizeKb * 1024;

        string content;
        bool isTruncated;

        if (originalSize <= maxBytes)
        {
            content = await File.ReadAllTextAsync(filePath, ct);
            isTruncated = false;
        }
        else
        {
            // Read up to max size
            using var reader = new StreamReader(filePath);
            var buffer = new char[maxBytes];
            var charsRead = await reader.ReadBlockAsync(buffer, ct);
            content = new string(buffer, 0, charsRead);
            isTruncated = true;

            // Try to truncate at a natural boundary (paragraph)
            content = TruncateAtBoundary(content);
        }

        return new DocumentContent
        {
            FilePath = filePath,
            Content = content,
            IsTruncated = isTruncated,
            OriginalSizeBytes = originalSize,
            LoadedSizeBytes = content.Length
        };
    }

    /// <summary>
    /// Reads a specific section of a document by heading.
    /// </summary>
    public async Task<SectionContent> ReadSectionAsync(
        string filePath,
        string headingPattern,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(headingPattern);

        var fullContent = await ReadAsync(filePath, ct);

        if (string.IsNullOrEmpty(fullContent.Content))
        {
            return new SectionContent
            {
                FilePath = filePath,
                Heading = headingPattern,
                Content = string.Empty,
                Found = false
            };
        }

        var section = ExtractSection(fullContent.Content, headingPattern);

        return new SectionContent
        {
            FilePath = filePath,
            Heading = headingPattern,
            Content = section ?? string.Empty,
            Found = section is not null
        };
    }

    /// <summary>
    /// Reads multiple sections from a document.
    /// </summary>
    public async Task<IReadOnlyList<SectionContent>> ReadSectionsAsync(
        string filePath,
        IEnumerable<string> headingPatterns,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(headingPatterns);

        var fullContent = await ReadAsync(filePath, ct);
        var results = new List<SectionContent>();

        foreach (var pattern in headingPatterns)
        {
            var section = ExtractSection(fullContent.Content, pattern);
            results.Add(new SectionContent
            {
                FilePath = filePath,
                Heading = pattern,
                Content = section ?? string.Empty,
                Found = section is not null
            });
        }

        return results;
    }

    /// <summary>
    /// Gets the list of headings in a document.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetHeadingsAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var content = await ReadAsync(filePath, ct);
        return ExtractHeadings(content.Content);
    }

    private static string TruncateAtBoundary(string content)
    {
        // Try to find a good truncation point
        var lines = content.Split('\n');

        // Find the last complete paragraph (empty line)
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) && i > lines.Length / 2)
            {
                return string.Join('\n', lines.Take(i)) + "\n\n[Content truncated...]";
            }
        }

        // Fallback: truncate at last newline
        var lastNewline = content.LastIndexOf('\n');
        if (lastNewline > content.Length / 2)
        {
            return content[..lastNewline] + "\n\n[Content truncated...]";
        }

        return content + "\n\n[Content truncated...]";
    }

    private static string? ExtractSection(string content, string headingPattern)
    {
        var lines = content.Split('\n');
        var patternLower = headingPattern.ToLowerInvariant().Trim();

        int sectionStart = -1;
        int sectionLevel = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith('#'))
            {
                continue;
            }

            // Count heading level
            var level = 0;
            while (level < line.Length && line[level] == '#')
            {
                level++;
            }

            var headingText = line[level..].Trim().ToLowerInvariant();

            if (sectionStart == -1)
            {
                // Looking for section start
                if (headingText.Contains(patternLower))
                {
                    sectionStart = i;
                    sectionLevel = level;
                }
            }
            else
            {
                // Looking for section end (same or higher level heading)
                if (level <= sectionLevel)
                {
                    return string.Join('\n', lines.Skip(sectionStart).Take(i - sectionStart));
                }
            }
        }

        // Section extends to end of document
        if (sectionStart >= 0)
        {
            return string.Join('\n', lines.Skip(sectionStart));
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractHeadings(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var headings = new List<string>();
        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                // Remove leading # and trim
                var heading = trimmed.TrimStart('#').Trim();
                if (!string.IsNullOrWhiteSpace(heading))
                {
                    headings.Add(heading);
                }
            }
        }

        return headings;
    }
}

/// <summary>
/// Result of reading a document.
/// </summary>
public sealed record DocumentContent
{
    public required string FilePath { get; init; }
    public required string Content { get; init; }
    public required bool IsTruncated { get; init; }
    public required long OriginalSizeBytes { get; init; }
    public required long LoadedSizeBytes { get; init; }
}

/// <summary>
/// Result of reading a document section.
/// </summary>
public sealed record SectionContent
{
    public required string FilePath { get; init; }
    public required string Heading { get; init; }
    public required string Content { get; init; }
    public required bool Found { get; init; }
}
