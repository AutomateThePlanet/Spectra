using Spectra.Core.Models;

namespace Spectra.CLI.Coverage;

/// <summary>
/// Analyzes coverage gaps by comparing documentation against existing tests.
/// </summary>
public sealed class GapAnalyzer
{
    /// <summary>
    /// Identifies coverage gaps by comparing documents against test source_refs.
    /// </summary>
    public IReadOnlyList<CoverageGap> AnalyzeGaps(
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests,
        string? focusArea = null)
    {
        // Collect all covered document paths from source_refs
        var coveredPaths = existingTests
            .SelectMany(t => t.SourceRefs ?? [])
            .Select(NormalizePath)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var gaps = new List<CoverageGap>();

        foreach (var doc in documentMap.Documents)
        {
            var normalizedPath = NormalizePath(doc.Path);

            // Skip if already covered
            if (coveredPaths.Contains(normalizedPath))
            {
                continue;
            }

            // Skip if focus area specified and doc doesn't match
            if (!string.IsNullOrEmpty(focusArea) && !MatchesFocus(doc, focusArea))
            {
                continue;
            }

            gaps.Add(new CoverageGap
            {
                DocumentPath = doc.Path,
                Section = null,
                Reason = $"No tests reference this document",
                Severity = EstimateSeverity(doc),
                Suggestion = $"Generate tests for: {doc.Title}"
            });
        }

        return gaps.OrderByDescending(g => g.Severity).ToList();
    }

    /// <summary>
    /// Gets remaining gaps after filtering out newly covered areas.
    /// </summary>
    public IReadOnlyList<CoverageGap> GetRemainingGaps(
        IReadOnlyList<CoverageGap> originalGaps,
        IReadOnlyList<TestCase> newTests)
    {
        var newlyCovered = newTests
            .SelectMany(t => t.SourceRefs ?? [])
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return originalGaps
            .Where(g => !newlyCovered.Contains(NormalizePath(g.DocumentPath)))
            .ToList();
    }

    private static GapSeverity EstimateSeverity(DocumentEntry doc)
    {
        // Estimate based on size and heading count
        var headingCount = doc.Headings?.Count ?? 0;

        if (doc.SizeKb > 10 || headingCount > 5)
        {
            return GapSeverity.High;
        }

        if (doc.SizeKb > 5 || headingCount > 2)
        {
            return GapSeverity.Medium;
        }

        return GapSeverity.Low;
    }

    private static bool MatchesFocus(DocumentEntry doc, string focusArea)
    {
        var lowerFocus = focusArea.ToLowerInvariant();

        // Check title, path, and preview for focus keywords
        if (doc.Title.ToLowerInvariant().Contains(lowerFocus))
        {
            return true;
        }

        if (doc.Path.ToLowerInvariant().Contains(lowerFocus))
        {
            return true;
        }

        if (doc.Preview.ToLowerInvariant().Contains(lowerFocus))
        {
            return true;
        }

        // Check headings
        if (doc.Headings?.Any(h => h.ToLowerInvariant().Contains(lowerFocus)) == true)
        {
            return true;
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
