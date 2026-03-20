using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Models;

namespace Spectra.Core.Index;

/// <summary>
/// Reads document index files in Markdown format.
/// </summary>
public sealed partial class DocumentIndexReader
{
    [GeneratedRegex(@"<!--\s*SPECTRA_INDEX_CHECKSUMS\s*\n(.+?)\n\s*-->", RegexOptions.Singleline)]
    private static partial Regex ChecksumCommentRegex();

    [GeneratedRegex(@"^###\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex EntryPathRegex();

    [GeneratedRegex(@"\*\*Title:\*\*\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\*\*Size:\*\*\s*(\d+)\s*KB\s*\|\s*\*\*Words:\*\*\s*([\d,]+)\s*\|\s*\*\*Tokens:\*\*\s*~([\d,]+)", RegexOptions.Multiline)]
    private static partial Regex StatsRegex();

    [GeneratedRegex(@"\*\*Last Modified:\*\*\s*(\d{4}-\d{2}-\d{2})", RegexOptions.Multiline)]
    private static partial Regex LastModifiedRegex();

    [GeneratedRegex(@"\*\*Key Entities:\*\*\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex KeyEntitiesRegex();

    [GeneratedRegex(@"Last updated:\s*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)")]
    private static partial Regex GeneratedAtRegex();

    /// <summary>
    /// Fast path: reads only the checksums from the index file.
    /// </summary>
    public async Task<Dictionary<string, string>?> ReadHashesOnlyAsync(
        string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return null;

        var content = await File.ReadAllTextAsync(path, ct);
        return ExtractChecksums(content);
    }

    /// <summary>
    /// Parses the full Markdown index back into a DocumentIndex model.
    /// </summary>
    public async Task<DocumentIndex?> ReadFullAsync(
        string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return null;

        var content = await File.ReadAllTextAsync(path, ct);
        return ParseFull(content);
    }

    public static Dictionary<string, string>? ExtractChecksums(string content)
    {
        var match = ChecksumCommentRegex().Match(content);
        if (!match.Success) return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(match.Groups[1].Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static DocumentIndex? ParseFull(string content)
    {
        var checksums = ExtractChecksums(content) ?? new Dictionary<string, string>();

        // Parse generated_at
        var generatedAt = DateTimeOffset.UtcNow;
        var genMatch = GeneratedAtRegex().Match(content);
        if (genMatch.Success && DateTimeOffset.TryParse(genMatch.Groups[1].Value, out var parsed))
        {
            generatedAt = parsed;
        }

        // Split into entry blocks by "### path"
        var entryMatches = EntryPathRegex().Matches(content);
        var entries = new List<DocumentIndexEntry>();

        for (var i = 0; i < entryMatches.Count; i++)
        {
            var entryMatch = entryMatches[i];
            var entryPath = entryMatch.Groups[1].Value.Trim();

            var blockStart = entryMatch.Index;
            var blockEnd = i + 1 < entryMatches.Count ? entryMatches[i + 1].Index : content.Length;
            var block = content[blockStart..blockEnd];

            var entry = ParseEntryBlock(block, entryPath, checksums);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return new DocumentIndex
        {
            GeneratedAt = generatedAt,
            TotalWordCount = entries.Sum(e => e.WordCount),
            TotalEstimatedTokens = entries.Sum(e => e.EstimatedTokens),
            Entries = entries
        };
    }

    private static DocumentIndexEntry? ParseEntryBlock(
        string block, string path, Dictionary<string, string> checksums)
    {
        var title = path;
        var titleMatch = TitleRegex().Match(block);
        if (titleMatch.Success) title = titleMatch.Groups[1].Value.Trim();

        int sizeKb = 0, wordCount = 0, estimatedTokens = 0;
        var statsMatch = StatsRegex().Match(block);
        if (statsMatch.Success)
        {
            int.TryParse(statsMatch.Groups[1].Value, out sizeKb);
            int.TryParse(statsMatch.Groups[2].Value.Replace(",", ""), out wordCount);
            int.TryParse(statsMatch.Groups[3].Value.Replace(",", ""), out estimatedTokens);
        }

        var lastModified = DateTimeOffset.UtcNow;
        var lmMatch = LastModifiedRegex().Match(block);
        if (lmMatch.Success && DateTimeOffset.TryParse(lmMatch.Groups[1].Value, out var lmParsed))
        {
            lastModified = lmParsed;
        }

        var keyEntities = new List<string>();
        var keMatch = KeyEntitiesRegex().Match(block);
        if (keMatch.Success)
        {
            keyEntities = keMatch.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        // Parse sections from table
        var sections = ParseSectionsTable(block);

        checksums.TryGetValue(path, out var hash);

        return new DocumentIndexEntry
        {
            Path = path,
            Title = title,
            Sections = sections,
            KeyEntities = keyEntities,
            WordCount = wordCount,
            EstimatedTokens = estimatedTokens,
            SizeKb = sizeKb,
            LastModified = lastModified,
            ContentHash = hash ?? ""
        };
    }

    private static IReadOnlyList<SectionSummary> ParseSectionsTable(string block)
    {
        var sections = new List<SectionSummary>();
        var lines = block.Split('\n');
        var inTable = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("| Section"))
            {
                inTable = true;
                continue;
            }

            if (inTable && trimmed.StartsWith("|---"))
            {
                continue;
            }

            if (inTable && trimmed.StartsWith('|'))
            {
                var cells = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length >= 2)
                {
                    var heading = cells[0].Trim();
                    var summary = cells[1].Trim().Replace("\\|", "|");

                    // Detect level from indentation prefix
                    var level = 2;
                    if (heading.Contains("↳"))
                    {
                        level = 3;
                        heading = heading.Replace("↳", "").Trim();
                    }

                    sections.Add(new SectionSummary
                    {
                        Heading = heading,
                        Level = level,
                        Summary = summary
                    });
                }
            }
            else if (inTable)
            {
                inTable = false;
            }
        }

        return sections;
    }
}
