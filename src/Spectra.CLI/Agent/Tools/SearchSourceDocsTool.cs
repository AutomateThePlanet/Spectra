using Spectra.CLI.Source;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that searches source documents for keywords.
/// </summary>
public sealed class SearchSourceDocsTool
{
    private readonly DocumentSearcher _searcher;

    public SearchSourceDocsTool(DocumentSearcher searcher)
    {
        _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "search_source_docs";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Searches source documents for keywords. Returns matching documents ranked by relevance. " +
        "Use this to find documentation related to specific features or topics.";

    /// <summary>
    /// Executes the tool and returns search results.
    /// </summary>
    public async Task<SearchSourceDocsResult> ExecuteAsync(
        string basePath,
        string keyword,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        try
        {
            var results = await _searcher.SearchAsync(basePath, keyword, maxResults, ct);

            return new SearchSourceDocsResult
            {
                Success = true,
                Keyword = keyword,
                Results = results.Select(r => new SearchResultItem
                {
                    FilePath = r.FilePath,
                    MatchCount = r.MatchCount,
                    Score = r.Score,
                    Excerpts = r.Matches.Take(3).Select(m => m.Context).ToList()
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            return new SearchSourceDocsResult
            {
                Success = false,
                Keyword = keyword,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Searches for multiple keywords with AND logic.
    /// </summary>
    public async Task<SearchSourceDocsResult> SearchAllAsync(
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
            return new SearchSourceDocsResult
            {
                Success = false,
                Error = "At least one keyword required"
            };
        }

        try
        {
            var results = await _searcher.SearchAllAsync(basePath, keywordList, maxResults, ct);

            return new SearchSourceDocsResult
            {
                Success = true,
                Keyword = string.Join(" AND ", keywordList),
                Results = results.Select(r => new SearchResultItem
                {
                    FilePath = r.FilePath,
                    MatchCount = r.MatchCount,
                    Score = r.Score,
                    Excerpts = r.Matches.Take(3).Select(m => m.Context).ToList()
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            return new SearchSourceDocsResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the search_source_docs tool.
/// </summary>
public sealed record SearchSourceDocsResult
{
    public required bool Success { get; init; }
    public string? Keyword { get; init; }
    public IReadOnlyList<SearchResultItem>? Results { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// A single search result item.
/// </summary>
public sealed record SearchResultItem
{
    public required string FilePath { get; init; }
    public required int MatchCount { get; init; }
    public required double Score { get; init; }
    public required IReadOnlyList<string> Excerpts { get; init; }
}
