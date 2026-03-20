using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Spectra.Core.Models;

namespace Spectra.Core.Parsing;

/// <summary>
/// Extracts rich metadata from documentation files for the document index.
/// </summary>
public sealed partial class DocumentIndexExtractor
{
    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex BacktickCodeRegex();

    [GeneratedRegex(@"\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)\b")]
    private static partial Regex CapitalizedPhraseRegex();

    [GeneratedRegex(@"/[a-z][a-z0-9/\-_.{}]*", RegexOptions.IgnoreCase)]
    private static partial Regex ApiPathRegex();

    [GeneratedRegex(@"""([^""]{2,50})""")]
    private static partial Regex QuotedStringRegex();

    /// <summary>
    /// Extracts a DocumentIndexEntry from file content and metadata.
    /// </summary>
    public DocumentIndexEntry Extract(string content, string relativePath, FileInfo fileInfo)
    {
        var title = ExtractTitle(content) ?? Path.GetFileNameWithoutExtension(relativePath);
        var sections = ExtractSections(content);
        var entities = ExtractKeyEntities(content);
        var wordCount = CountWords(content);
        var estimatedTokens = (int)(wordCount * 1.3);
        var sizeKb = (int)Math.Ceiling(fileInfo.Length / 1024.0);
        var hash = ComputeHash(content);

        return new DocumentIndexEntry
        {
            Path = relativePath,
            Title = title,
            Sections = sections,
            KeyEntities = entities,
            WordCount = wordCount,
            EstimatedTokens = estimatedTokens,
            SizeKb = sizeKb,
            LastModified = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
            ContentHash = hash
        };
    }

    /// <summary>
    /// Extracts a DocumentIndexEntry from a file on disk.
    /// </summary>
    public async Task<DocumentIndexEntry> ExtractFromFileAsync(
        string absolutePath, string relativePath, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(absolutePath, ct);
        var fileInfo = new FileInfo(absolutePath);
        return Extract(content, relativePath, fileInfo);
    }

    /// <summary>
    /// Computes SHA-256 hash of file content.
    /// </summary>
    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? ExtractTitle(string content)
    {
        var match = DocumentMapExtractor.TitleRegex().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static IReadOnlyList<SectionSummary> ExtractSections(string content)
    {
        var sections = new List<SectionSummary>();

        // Collect all H2 and H3 headings with their positions
        var headings = new List<(int Index, int EndOfLine, string Heading, int Level)>();

        foreach (Match m in DocumentMapExtractor.H2Regex().Matches(content))
        {
            headings.Add((m.Index, m.Index + m.Length, m.Groups[1].Value.Trim(), 2));
        }

        foreach (Match m in DocumentMapExtractor.H3Regex().Matches(content))
        {
            headings.Add((m.Index, m.Index + m.Length, m.Groups[1].Value.Trim(), 3));
        }

        headings.Sort((a, b) => a.Index.CompareTo(b.Index));

        for (var i = 0; i < headings.Count; i++)
        {
            var (_, endOfLine, heading, level) = headings[i];
            var nextStart = i + 1 < headings.Count ? headings[i + 1].Index : content.Length;
            var body = content[endOfLine..nextStart].Trim();
            var summary = ExtractSummary(body);

            sections.Add(new SectionSummary
            {
                Heading = heading,
                Level = level,
                Summary = summary
            });
        }

        return sections;
    }

    private static string ExtractSummary(string body)
    {
        // Strip markdown headings and collapse whitespace
        var cleaned = Regex.Replace(body, @"^#+\s+.*$", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned.Length <= 200 ? cleaned : cleaned[..200] + "...";
    }

    private static IReadOnlyList<string> ExtractKeyEntities(string content)
    {
        var entities = new HashSet<string>(StringComparer.Ordinal);

        // Backtick code spans
        foreach (Match m in BacktickCodeRegex().Matches(content))
        {
            var val = m.Groups[1].Value.Trim();
            if (val.Length >= 2 && val.Length <= 60)
            {
                entities.Add(val);
            }
        }

        // Capitalized multi-word phrases
        foreach (Match m in CapitalizedPhraseRegex().Matches(content))
        {
            entities.Add(m.Value);
        }

        // API paths
        foreach (Match m in ApiPathRegex().Matches(content))
        {
            entities.Add(m.Value);
        }

        // Quoted strings
        foreach (Match m in QuotedStringRegex().Matches(content))
        {
            entities.Add(m.Groups[1].Value);
        }

        // Limit to top 20 by frequency-ish (first seen order, capped)
        return entities.Take(20).ToList();
    }

    private static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
