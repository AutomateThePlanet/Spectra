using Spectra.Core.Models;

namespace Spectra.Core.Validation;

/// <summary>
/// Detects potential duplicate test cases based on title and step similarity.
/// </summary>
public sealed class DuplicateDetector
{
    private readonly double _threshold;

    /// <summary>
    /// Creates a new DuplicateDetector with the specified similarity threshold.
    /// </summary>
    /// <param name="threshold">Similarity threshold (0.0 to 1.0). Default is 0.6.</param>
    public DuplicateDetector(double threshold = 0.6)
    {
        if (threshold < 0.0 || threshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 0.0 and 1.0");
        }

        _threshold = threshold;
    }

    /// <summary>
    /// Finds potential duplicates for a test case within a collection.
    /// </summary>
    public IReadOnlyList<DuplicateMatch> FindDuplicates(TestCase candidate, IEnumerable<TestCase> existingTests)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existingTests);

        var matches = new List<DuplicateMatch>();

        foreach (var existing in existingTests)
        {
            if (existing.Id == candidate.Id)
            {
                continue; // Skip self-comparison
            }

            var titleSimilarity = CalculateTitleSimilarity(candidate.Title, existing.Title);
            var stepSimilarity = CalculateStepSimilarity(candidate.Steps, existing.Steps);

            // Combined similarity (weighted: title 60%, steps 40%)
            var combinedSimilarity = (titleSimilarity * 0.6) + (stepSimilarity * 0.4);

            if (combinedSimilarity >= _threshold)
            {
                matches.Add(new DuplicateMatch(
                    existing.Id,
                    existing.Title,
                    combinedSimilarity,
                    titleSimilarity,
                    stepSimilarity));
            }
        }

        return matches.OrderByDescending(m => m.Similarity).ToList();
    }

    /// <summary>
    /// Checks if a test case is a potential duplicate of any existing test.
    /// </summary>
    public bool IsPotentialDuplicate(TestCase candidate, IEnumerable<TestCase> existingTests)
    {
        return FindDuplicates(candidate, existingTests).Count > 0;
    }

    /// <summary>
    /// Finds all duplicate pairs within a collection of test cases.
    /// </summary>
    public IReadOnlyList<DuplicatePair> FindAllDuplicatePairs(IEnumerable<TestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);

        var testList = tests.ToList();
        var pairs = new List<DuplicatePair>();
        var seen = new HashSet<string>();

        for (var i = 0; i < testList.Count; i++)
        {
            for (var j = i + 1; j < testList.Count; j++)
            {
                var test1 = testList[i];
                var test2 = testList[j];

                var pairKey = $"{test1.Id}:{test2.Id}";
                if (seen.Contains(pairKey))
                {
                    continue;
                }

                var titleSimilarity = CalculateTitleSimilarity(test1.Title, test2.Title);
                var stepSimilarity = CalculateStepSimilarity(test1.Steps, test2.Steps);
                var combinedSimilarity = (titleSimilarity * 0.6) + (stepSimilarity * 0.4);

                if (combinedSimilarity >= _threshold)
                {
                    pairs.Add(new DuplicatePair(
                        test1.Id,
                        test2.Id,
                        combinedSimilarity,
                        titleSimilarity,
                        stepSimilarity));
                    seen.Add(pairKey);
                }
            }
        }

        return pairs.OrderByDescending(p => p.Similarity).ToList();
    }

    /// <summary>
    /// Calculates similarity between two titles using Levenshtein distance.
    /// </summary>
    public double CalculateTitleSimilarity(string title1, string title2)
    {
        if (string.IsNullOrWhiteSpace(title1) || string.IsNullOrWhiteSpace(title2))
        {
            return 0.0;
        }

        // Normalize titles
        var normalized1 = NormalizeText(title1);
        var normalized2 = NormalizeText(title2);

        if (normalized1 == normalized2)
        {
            return 1.0;
        }

        var distance = LevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);

        if (maxLength == 0)
        {
            return 1.0;
        }

        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Calculates similarity between two step lists using Jaccard similarity.
    /// </summary>
    public double CalculateStepSimilarity(IReadOnlyList<string> steps1, IReadOnlyList<string> steps2)
    {
        if (steps1.Count == 0 && steps2.Count == 0)
        {
            return 1.0;
        }

        if (steps1.Count == 0 || steps2.Count == 0)
        {
            return 0.0;
        }

        // Normalize steps
        var normalized1 = steps1.Select(NormalizeText).ToHashSet();
        var normalized2 = steps2.Select(NormalizeText).ToHashSet();

        // Calculate Jaccard similarity
        var intersection = normalized1.Intersect(normalized2).Count();
        var union = normalized1.Union(normalized2).Count();

        if (union == 0)
        {
            return 1.0;
        }

        return (double)intersection / union;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Convert to lowercase, remove punctuation, collapse whitespace
        var normalized = text.ToLowerInvariant();
        normalized = new string(normalized.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        normalized = string.Join(" ", normalized.Split([' '], StringSplitOptions.RemoveEmptyEntries));

        return normalized;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var n = s1.Length;
        var m = s2.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

/// <summary>
/// Represents a potential duplicate match.
/// </summary>
public sealed record DuplicateMatch(
    string MatchedTestId,
    string MatchedTestTitle,
    double Similarity,
    double TitleSimilarity,
    double StepSimilarity);

/// <summary>
/// Represents a pair of potentially duplicate tests.
/// </summary>
public sealed record DuplicatePair(
    string TestId1,
    string TestId2,
    double Similarity,
    double TitleSimilarity,
    double StepSimilarity);
