using System.Text.RegularExpressions;
using Spectra.Core.Models;
using Spectra.Core.Models.Index;

namespace Spectra.Core.Index;

/// <summary>
/// Reads a per-suite <c>groups/{id}.index.md</c> file (Spec 040 v2 layout).
/// Reuses the per-document parsing regex set shared with the legacy
/// <see cref="DocumentIndexReader"/>.
/// </summary>
public sealed partial class SuiteIndexFileReader
{
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

    [GeneratedRegex(@"Last indexed:\s*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)")]
    private static partial Regex GeneratedAtRegex();

    [GeneratedRegex(@"~([\d,]+)\s*tokens", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderTokensRegex();

    /// <summary>
    /// Reads a suite index file. Returns null if absent.
    /// </summary>
    public async Task<SuiteIndexFile?> ReadAsync(string path, string suiteId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(suiteId);
        if (!File.Exists(path)) return null;

        var content = await File.ReadAllTextAsync(path, ct);
        return Parse(content, suiteId);
    }

    /// <summary>
    /// Parses a suite index file's content. <paramref name="suiteId"/> is supplied
    /// by the caller (the file is identified by its filename, not by content).
    /// </summary>
    public static SuiteIndexFile Parse(string content, string suiteId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(suiteId);

        var generatedAt = DateTimeOffset.UtcNow;
        var genMatch = GeneratedAtRegex().Match(content);
        if (genMatch.Success && DateTimeOffset.TryParse(genMatch.Groups[1].Value, out var parsed))
        {
            generatedAt = parsed;
        }

        var headerTokens = 0;
        var headerTokenMatch = HeaderTokensRegex().Match(content);
        if (headerTokenMatch.Success)
        {
            int.TryParse(headerTokenMatch.Groups[1].Value.Replace(",", ""), out headerTokens);
        }

        var entryMatches = EntryPathRegex().Matches(content);
        var entries = new List<DocumentIndexEntry>();

        for (var i = 0; i < entryMatches.Count; i++)
        {
            var entryMatch = entryMatches[i];
            var entryPath = entryMatch.Groups[1].Value.Trim();

            var blockStart = entryMatch.Index;
            var blockEnd = i + 1 < entryMatches.Count
                ? entryMatches[i + 1].Index
                : content.Length;
            var block = content[blockStart..blockEnd];

            var entry = ParseEntryBlock(block, entryPath);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        var totalTokens = headerTokens > 0
            ? headerTokens
            : entries.Sum(e => e.EstimatedTokens);

        return new SuiteIndexFile
        {
            SuiteId = suiteId,
            GeneratedAt = generatedAt,
            DocumentCount = entries.Count,
            TokensEstimated = totalTokens,
            Entries = entries,
        };
    }

    private static DocumentIndexEntry? ParseEntryBlock(string block, string path)
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

        var sections = ParseSectionsTable(block);

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
            ContentHash = "", // checksums live in _checksums.json, not in the suite file
        };
    }

    /// <summary>Sentinel for <c>\|</c> escape during table-row tokenization.</summary>
    private const char EscapedPipePlaceholder = '';

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
                // Honor the writer's `\|` escape so cell contents that contain a
                // pipe round-trip cleanly. Split on raw pipes, then restore.
                var protectedLine = trimmed.Replace("\\|", EscapedPipePlaceholder.ToString());
                var cells = protectedLine.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length >= 2)
                {
                    var heading = cells[0].Trim().Replace(EscapedPipePlaceholder, '|');
                    var summary = cells[1].Trim().Replace(EscapedPipePlaceholder, '|');

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
                        Summary = summary,
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
