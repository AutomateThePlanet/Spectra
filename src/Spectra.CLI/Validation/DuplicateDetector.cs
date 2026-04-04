using Spectra.Core.Models;

namespace Spectra.CLI.Validation;

/// <summary>
/// Detects duplicate tests using normalized Levenshtein similarity.
/// </summary>
public sealed class DuplicateDetector
{
    private readonly double _threshold;

    public DuplicateDetector(double threshold = 0.8)
    {
        _threshold = threshold;
    }

    /// <summary>
    /// Finds existing tests with titles similar to the given title.
    /// Returns matches above the similarity threshold.
    /// </summary>
    public List<DuplicateMatch> FindDuplicates(string title, IReadOnlyList<TestCase> existingTests)
    {
        var normalizedTitle = Normalize(title);
        var matches = new List<DuplicateMatch>();

        foreach (var test in existingTests)
        {
            var normalizedExisting = Normalize(test.Title);
            var similarity = ComputeSimilarity(normalizedTitle, normalizedExisting);

            if (similarity >= _threshold)
            {
                matches.Add(new DuplicateMatch
                {
                    ExistingTestId = test.Id,
                    ExistingTitle = test.Title,
                    Similarity = similarity
                });
            }
        }

        return matches.OrderByDescending(m => m.Similarity).ToList();
    }

    /// <summary>
    /// Computes normalized similarity between two strings (0.0 to 1.0).
    /// </summary>
    public static double ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return 1.0 - (double)distance / maxLen;
    }

    private static string Normalize(string text)
    {
        return text.Trim().ToLowerInvariant();
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        var prev = new int[m + 1];
        var curr = new int[m + 1];

        for (var j = 0; j <= m; j++)
            prev[j] = j;

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }
}

/// <summary>
/// A match found by duplicate detection.
/// </summary>
public sealed class DuplicateMatch
{
    public required string ExistingTestId { get; init; }
    public required string ExistingTitle { get; init; }
    public double Similarity { get; init; }
}
