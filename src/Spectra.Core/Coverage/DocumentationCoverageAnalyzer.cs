using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Coverage;

/// <summary>
/// Analyzes documentation coverage: which docs have linked tests via source_refs.
/// </summary>
public sealed class DocumentationCoverageAnalyzer
{
    /// <summary>
    /// Analyzes documentation coverage from a document map and all parsed tests.
    /// </summary>
    public DocumentationCoverage Analyze(
        DocumentMap documentMap,
        IReadOnlyList<TestCase> allTests)
    {
        var details = new List<DocumentCoverageDetail>();

        foreach (var doc in documentMap.Documents)
        {
            var testsForDoc = allTests
                .Where(t => t.SourceRefs.Any(r => StripFragment(r) == doc.Path))
                .ToList();

            details.Add(new DocumentCoverageDetail
            {
                Doc = doc.Path,
                TestCount = testsForDoc.Count,
                Covered = testsForDoc.Count > 0,
                TestIds = testsForDoc.Select(t => t.Id).ToList()
            });
        }

        var totalDocs = details.Count;
        var coveredDocs = details.Count(d => d.Covered);
        var percentage = totalDocs > 0
            ? Math.Round((coveredDocs * 100m) / totalDocs, 2)
            : 0m;

        return new DocumentationCoverage
        {
            TotalDocs = totalDocs,
            CoveredDocs = coveredDocs,
            Percentage = percentage,
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
