using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Source;

namespace Spectra.Core.Coverage;

/// <summary>
/// Analyzes documentation coverage: which docs have linked tests via source_refs.
/// </summary>
public sealed class DocumentationCoverageAnalyzer
{
    /// <summary>
    /// Analyzes documentation coverage from a document map and all parsed tests.
    /// </summary>
    /// <param name="documentMap">All discovered documents.</param>
    /// <param name="allTests">All parsed test cases.</param>
    /// <param name="coverageExcludePatterns">
    /// Optional coverage-scoped exclusion globs (Spec 060,
    /// <c>coverage.coverage_exclude_patterns</c>). Matched documents are dropped
    /// from the coverage denominator and reported with a distinct "excluded"
    /// status, but remain in <paramref name="documentMap"/> (this method never
    /// mutates the map). Null or empty reproduces the pre-Spec-060 behavior
    /// exactly.
    /// </param>
    public DocumentationCoverage Analyze(
        DocumentMap documentMap,
        IReadOnlyList<TestCase> allTests,
        IReadOnlyList<string>? coverageExcludePatterns = null)
    {
        var matcher = coverageExcludePatterns is { Count: > 0 }
            ? new ExclusionPatternMatcher(coverageExcludePatterns)
            : null;

        var details = new List<DocumentCoverageDetail>();

        foreach (var doc in documentMap.Documents)
        {
            var testsForDoc = allTests
                .Where(t => t.SourceRefs.Any(r => StripFragment(r) == doc.Path))
                .ToList();

            // Coverage-scoped exclusion (Spec 060): excluded docs stay listed
            // but drop out of the denominator. Exclusion takes precedence over
            // covered/uncovered classification.
            string? matchedPattern = null;
            var excluded = matcher is not null
                           && matcher.IsExcluded(doc.Path, out matchedPattern);

            details.Add(new DocumentCoverageDetail
            {
                Doc = doc.Path,
                TestCount = testsForDoc.Count,
                Covered = !excluded && testsForDoc.Count > 0,
                TestIds = testsForDoc.Select(t => t.Id).ToList(),
                Excluded = excluded,
                ExcludedByPattern = matchedPattern
            });
        }

        var excludedDocs = details.Count(d => d.Excluded);
        // Denominator counts only non-excluded docs.
        var totalDocs = details.Count - excludedDocs;
        var coveredDocs = details.Count(d => d.Covered);
        var percentage = totalDocs > 0
            ? Math.Round((coveredDocs * 100m) / totalDocs, 2)
            : 0m;

        var undocumentedTests = allTests
            .Where(t => t.SourceRefs.Count == 0)
            .Select(t => t.Id)
            .ToList();

        return new DocumentationCoverage
        {
            TotalDocs = totalDocs,
            CoveredDocs = coveredDocs,
            Percentage = percentage,
            ExcludedDocs = excludedDocs,
            UndocumentedTestCount = undocumentedTests.Count,
            UndocumentedTestIds = undocumentedTests,
            Details = details
        };
    }

    /// <summary>
    /// Strips the fragment anchor (#section) from a source_ref path.
    /// e.g. "docs/auth.md#Login-Flow" → "docs/auth.md"
    /// </summary>
    private static string StripFragment(string sourceRef)
    {
        var hashIndex = sourceRef.IndexOf('#');
        return hashIndex >= 0 ? sourceRef[..hashIndex] : sourceRef;
    }
}
