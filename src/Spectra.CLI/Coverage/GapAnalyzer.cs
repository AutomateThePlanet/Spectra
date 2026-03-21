using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.CLI.Coverage;

/// <summary>
/// Analyzes coverage gaps by comparing documentation against existing tests.
/// </summary>
public sealed class GapAnalyzer
{
    /// <summary>
    /// Identifies coverage gaps by comparing documents against test source_refs
    /// and analyzing section-level coverage.
    /// </summary>
    public IReadOnlyList<CoverageGap> AnalyzeGaps(
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests,
        string? focusArea = null,
        string? suiteName = null)
    {
        // Collect all covered document paths from source_refs
        var coveredPaths = existingTests
            .SelectMany(t => t.SourceRefs ?? [])
            .Select(NormalizePath)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build a searchable index of test coverage (titles + steps)
        var testCoverageTerms = BuildCoverageIndex(existingTests);

        var gaps = new List<CoverageGap>();

        foreach (var doc in documentMap.Documents)
        {
            var normalizedPath = NormalizePath(doc.Path);

            // Skip if suite name specified and doc doesn't match suite context
            if (!string.IsNullOrEmpty(suiteName) && !MatchesSuite(doc, suiteName))
            {
                continue;
            }

            // Skip if focus area specified and doc doesn't match
            if (!string.IsNullOrEmpty(focusArea) && !MatchesFocus(doc, focusArea))
            {
                continue;
            }

            // Check if document is covered at path level
            var isDocCovered = coveredPaths.Contains(normalizedPath);

            if (!isDocCovered)
            {
                // Document not referenced at all - add document-level gap
                gaps.Add(new CoverageGap
                {
                    DocumentPath = doc.Path,
                    Section = null,
                    Reason = "No tests reference this document",
                    Severity = EstimateSeverity(doc),
                    Suggestion = $"Generate tests for: {doc.Title}"
                });
            }
            else if (doc.Headings?.Count > 0)
            {
                // Document is referenced - check section-level coverage
                var uncoveredSections = FindUncoveredSections(doc, testCoverageTerms);

                foreach (var section in uncoveredSections)
                {
                    gaps.Add(new CoverageGap
                    {
                        DocumentPath = doc.Path,
                        Section = section,
                        Reason = $"Section '{section}' has no matching tests",
                        Severity = GapSeverity.Medium,
                        Suggestion = $"Generate tests for: {section}"
                    });
                }
            }
        }

        return gaps.OrderByDescending(g => g.Severity).ToList();
    }

    /// <summary>
    /// Builds a searchable index of terms covered by existing tests.
    /// </summary>
    private static HashSet<string> BuildCoverageIndex(IReadOnlyList<TestCase> tests)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var test in tests)
        {
            // Add normalized words from title
            AddNormalizedTerms(terms, test.Title);

            // Add normalized words from steps
            foreach (var step in test.Steps)
            {
                AddNormalizedTerms(terms, step);
            }

            // Add normalized words from expected result
            if (!string.IsNullOrEmpty(test.ExpectedResult))
            {
                AddNormalizedTerms(terms, test.ExpectedResult);
            }

            // Add tags
            foreach (var tag in test.Tags)
            {
                terms.Add(tag.ToLowerInvariant());
            }
        }

        return terms;
    }

    private static void AddNormalizedTerms(HashSet<string> terms, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Split on common separators and add meaningful words
        var words = text.ToLowerInvariant()
            .Split([' ', '-', '_', '.', ',', ':', ';', '(', ')', '[', ']', '"', '\'', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2); // Skip very short words

        foreach (var word in words)
        {
            terms.Add(word);
        }
    }

    /// <summary>
    /// Finds sections in a document that don't appear to be covered by tests.
    /// </summary>
    private static List<string> FindUncoveredSections(DocumentEntry doc, HashSet<string> coverageTerms)
    {
        var uncovered = new List<string>();

        if (doc.Headings is null) return uncovered;

        foreach (var heading in doc.Headings)
        {
            // Check if any significant words from the heading appear in test coverage
            var headingWords = heading.ToLowerInvariant()
                .Split([' ', '-', '_', '.', ':', '(', ')', '[', ']'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToList();

            // Skip if no meaningful words
            if (headingWords.Count == 0) continue;

            // Skip common generic headings
            var skipHeadings = new[] { "overview", "introduction", "summary", "references", "appendix", "changelog", "prerequisites", "requirements" };
            if (skipHeadings.Any(s => heading.ToLowerInvariant().Contains(s))) continue;

            // Check if at least one significant word is covered
            var isCovered = headingWords.Any(w => coverageTerms.Contains(w));

            if (!isCovered)
            {
                uncovered.Add(heading);
            }
        }

        return uncovered;
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

    private static bool MatchesSuite(DocumentEntry doc, string suiteName)
    {
        var lowerSuite = suiteName.ToLowerInvariant();

        // Split suite name into words for matching (e.g., "citizen-portal" -> ["citizen", "portal"])
        var suiteWords = lowerSuite
            .Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();

        if (suiteWords.Count == 0)
        {
            suiteWords.Add(lowerSuite);
        }

        var docTitle = doc.Title.ToLowerInvariant();
        var docPath = doc.Path.ToLowerInvariant();
        var docPreview = doc.Preview.ToLowerInvariant();

        // Check if any suite word appears in the document
        foreach (var word in suiteWords)
        {
            if (docTitle.Contains(word) || docPath.Contains(word) || docPreview.Contains(word))
            {
                return true;
            }

            // Check headings
            if (doc.Headings?.Any(h => h.ToLowerInvariant().Contains(word)) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return SourceRefNormalizer.NormalizePath(path);
    }
}
