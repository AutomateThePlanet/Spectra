using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Source;

/// <summary>
/// Searches for keywords across source documentation.
/// </summary>
public sealed class DocumentSearcher
{
    private readonly SourceDiscovery _discovery;
    private readonly SourceDocumentReader _reader;

    public DocumentSearcher(SourceConfig? config = null)
    {
        _discovery = new SourceDiscovery(config);
        _reader = new SourceDocumentReader(config);
    }

    /// <summary>
    /// Searches for a keyword across all documents.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string basePath,
        string keyword,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        var results = new List<SearchResult>();
        var keywordLower = keyword.ToLowerInvariant();

        foreach (var (absolutePath, relativePath) in _discovery.DiscoverWithRelativePaths(basePath))
        {
            ct.ThrowIfCancellationRequested();

            var content = await _reader.ReadAsync(absolutePath, ct);

            if (string.IsNullOrEmpty(content.Content))
            {
                continue;
            }

            var matches = FindMatches(content.Content, keywordLower);

            if (matches.Count > 0)
            {
                results.Add(new SearchResult
                {
                    FilePath = relativePath,
                    Matches = matches,
                    MatchCount = matches.Count,
                    Score = CalculateScore(content.Content, keywordLower, matches.Count)
                });
            }
        }

        // Sort by score descending and limit results
        return results
            .OrderByDescending(r => r.Score)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Searches for multiple keywords (AND logic).
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAllAsync(
        string basePath,
        IEnumerable<string> keywords,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(keywords);

        var keywordList = keywords.ToList();
        if (keywordList.Count == 0)
        {
            return [];
        }

        var results = new List<SearchResult>();
        var keywordsLower = keywordList.Select(k => k.ToLowerInvariant()).ToList();

        foreach (var (absolutePath, relativePath) in _discovery.DiscoverWithRelativePaths(basePath))
        {
            ct.ThrowIfCancellationRequested();

            var content = await _reader.ReadAsync(absolutePath, ct);

            if (string.IsNullOrEmpty(content.Content))
            {
                continue;
            }

            var contentLower = content.Content.ToLowerInvariant();
            var allMatches = new List<SearchMatch>();
            var missingKeyword = false;

            foreach (var keyword in keywordsLower)
            {
                var matches = FindMatches(content.Content, keyword);

                if (matches.Count == 0)
                {
                    missingKeyword = true;
                    break;
                }

                allMatches.AddRange(matches);
            }

            if (missingKeyword)
            {
                continue;
            }

            results.Add(new SearchResult
            {
                FilePath = relativePath,
                Matches = allMatches.OrderBy(m => m.Position).ToList(),
                MatchCount = allMatches.Count,
                Score = CalculateMultiKeywordScore(content.Content, keywordsLower, allMatches.Count)
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Searches for any of the keywords (OR logic).
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAnyAsync(
        string basePath,
        IEnumerable<string> keywords,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(keywords);

        var keywordList = keywords.ToList();
        if (keywordList.Count == 0)
        {
            return [];
        }

        var results = new List<SearchResult>();
        var keywordsLower = keywordList.Select(k => k.ToLowerInvariant()).ToList();

        foreach (var (absolutePath, relativePath) in _discovery.DiscoverWithRelativePaths(basePath))
        {
            ct.ThrowIfCancellationRequested();

            var content = await _reader.ReadAsync(absolutePath, ct);

            if (string.IsNullOrEmpty(content.Content))
            {
                continue;
            }

            var allMatches = new List<SearchMatch>();

            foreach (var keyword in keywordsLower)
            {
                var matches = FindMatches(content.Content, keyword);
                allMatches.AddRange(matches);
            }

            if (allMatches.Count > 0)
            {
                results.Add(new SearchResult
                {
                    FilePath = relativePath,
                    Matches = allMatches
                        .DistinctBy(m => m.Position)
                        .OrderBy(m => m.Position)
                        .ToList(),
                    MatchCount = allMatches.Count,
                    Score = CalculateMultiKeywordScore(content.Content, keywordsLower, allMatches.Count)
                });
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(maxResults)
            .ToList();
    }

    private static List<SearchMatch> FindMatches(string content, string keywordLower)
    {
        var matches = new List<SearchMatch>();
        var contentLower = content.ToLowerInvariant();

        var index = 0;
        while ((index = contentLower.IndexOf(keywordLower, index, StringComparison.Ordinal)) >= 0)
        {
            var context = ExtractContext(content, index, keywordLower.Length);
            var lineNumber = CountLines(content, index);

            matches.Add(new SearchMatch
            {
                Position = index,
                LineNumber = lineNumber,
                Context = context,
                MatchedText = content.Substring(index, keywordLower.Length)
            });

            index += keywordLower.Length;
        }

        return matches;
    }

    private static string ExtractContext(string content, int matchIndex, int matchLength, int contextChars = 50)
    {
        var start = Math.Max(0, matchIndex - contextChars);
        var end = Math.Min(content.Length, matchIndex + matchLength + contextChars);

        var context = content[start..end];

        // Trim to word boundaries
        if (start > 0)
        {
            var firstSpace = context.IndexOf(' ');
            if (firstSpace > 0 && firstSpace < contextChars / 2)
            {
                context = "..." + context[(firstSpace + 1)..];
            }
            else
            {
                context = "..." + context;
            }
        }

        if (end < content.Length)
        {
            var lastSpace = context.LastIndexOf(' ');
            if (lastSpace > context.Length - contextChars / 2)
            {
                context = context[..lastSpace] + "...";
            }
            else
            {
                context += "...";
            }
        }

        return context.Replace('\n', ' ').Replace('\r', ' ');
    }

    private static int CountLines(string content, int upToIndex)
    {
        var count = 1;
        for (var i = 0; i < upToIndex; i++)
        {
            if (content[i] == '\n')
            {
                count++;
            }
        }
        return count;
    }

    private static double CalculateScore(string content, string keyword, int matchCount)
    {
        var contentLength = content.Length;
        var keywordLength = keyword.Length;

        // Base score from match density
        var density = (double)(matchCount * keywordLength) / contentLength;

        // Boost for shorter documents (more focused)
        var lengthBoost = Math.Min(1.0, 10000.0 / contentLength);

        // Boost for title matches
        var titleBoost = 1.0;
        var firstLine = content.Split('\n').FirstOrDefault() ?? "";
        if (firstLine.ToLowerInvariant().Contains(keyword))
        {
            titleBoost = 2.0;
        }

        return (density * 100 + matchCount) * lengthBoost * titleBoost;
    }

    private static double CalculateMultiKeywordScore(string content, List<string> keywords, int totalMatchCount)
    {
        var baseScore = CalculateScore(content, keywords[0], totalMatchCount);

        // Bonus for matching multiple keywords
        var keywordBonus = keywords.Count > 1 ? keywords.Count * 0.5 : 1.0;

        return baseScore * keywordBonus;
    }
}

/// <summary>
/// Result of a document search.
/// </summary>
public sealed record SearchResult
{
    public required string FilePath { get; init; }
    public required IReadOnlyList<SearchMatch> Matches { get; init; }
    public required int MatchCount { get; init; }
    public required double Score { get; init; }
}

/// <summary>
/// A single match within a document.
/// </summary>
public sealed record SearchMatch
{
    public required int Position { get; init; }
    public required int LineNumber { get; init; }
    public required string Context { get; init; }
    public required string MatchedText { get; init; }
}
