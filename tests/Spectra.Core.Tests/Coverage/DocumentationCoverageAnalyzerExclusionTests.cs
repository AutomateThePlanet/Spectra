using System.Text.Json;
using Spectra.Core.Coverage;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Coverage;

/// <summary>
/// Spec 060 — coverage-scoped document exclusions. Verifies the denominator
/// drops excluded docs, excluded docs stay visible and in the map, no-config
/// behavior is unchanged, and the three exclusion concepts stay independent.
/// </summary>
public class DocumentationCoverageAnalyzerExclusionTests
{
    private readonly DocumentationCoverageAnalyzer _analyzer = new();

    // ---- US1: denominator filtering (FR-002, SC-001) ----

    [Fact]
    public void Analyze_ExcludePattern_DropsMatchedDocsFromDenominator()
    {
        var docMap = BuildMap(
            "docs/login.md",
            "docs/billing.md",
            "docs/release-notes/v1.md",
            "docs/release-notes/v2.md");

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["docs/login.md"])
            // billing uncovered; release-notes excluded
        };

        var result = _analyzer.Analyze(docMap, tests, ["docs/release-notes/**"]);

        // Denominator = 2 in-scope docs (login, billing); 2 release-notes excluded.
        Assert.Equal(2, result.TotalDocs);
        Assert.Equal(2, result.ExcludedDocs);
        Assert.Equal(1, result.CoveredDocs);
        Assert.Equal(50m, result.Percentage);
    }

    [Fact]
    public void Analyze_ExcludedDocs_NotCountedAsCoveredEvenWithTests()
    {
        // Edge case: an excluded doc that DOES have linked tests is still excluded.
        var docMap = BuildMap("docs/login.md", "docs/release-notes/v1.md");

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["docs/login.md"]),
            CreateTestCase("TC-002", ["docs/release-notes/v1.md"]) // tests exist but doc excluded
        };

        var result = _analyzer.Analyze(docMap, tests, ["docs/release-notes/**"]);

        Assert.Equal(1, result.TotalDocs);   // only login in denominator
        Assert.Equal(1, result.CoveredDocs);
        Assert.Equal(100m, result.Percentage);

        var rn = result.Details.First(d => d.Doc == "docs/release-notes/v1.md");
        Assert.True(rn.Excluded);
        Assert.False(rn.Covered);            // exclusion precedence over covered
        Assert.Equal(1, rn.TestCount);       // test linkage still reported
    }

    [Fact]
    public void Analyze_DoesNotMutateInputDocumentMap()
    {
        // FR-002 / SC-003: excluded docs must remain in the map for gen/analysis/indexing.
        var docMap = BuildMap("docs/login.md", "docs/release-notes/v1.md");
        var originalPaths = docMap.Documents.Select(d => d.Path).ToList();

        _ = _analyzer.Analyze(docMap, [], ["docs/release-notes/**"]);

        Assert.Equal(2, docMap.Documents.Count);
        Assert.Equal(originalPaths, docMap.Documents.Select(d => d.Path).ToList());
        // And excluded docs still appear in the analyzer's per-doc details (not dropped).
        var result = _analyzer.Analyze(docMap, [], ["docs/release-notes/**"]);
        Assert.Contains(result.Details, d => d.Doc == "docs/release-notes/v1.md" && d.Excluded);
    }

    // ---- US2 (analyzer side): excluded status surfaced with pattern (FR-003) ----

    [Fact]
    public void Analyze_ExcludedDoc_ReportsMatchedPattern()
    {
        var docMap = BuildMap("docs/login.md", "docs/SUMMARY.md");

        var result = _analyzer.Analyze(docMap, [], ["**/SUMMARY.md"]);

        var summary = result.Details.First(d => d.Doc == "docs/SUMMARY.md");
        Assert.True(summary.Excluded);
        Assert.Equal("**/SUMMARY.md", summary.ExcludedByPattern);

        var login = result.Details.First(d => d.Doc == "docs/login.md");
        Assert.False(login.Excluded);
        Assert.Null(login.ExcludedByPattern);
    }

    // ---- US3: no-config equivalence (FR-005, SC-004) ----

    [Fact]
    public void Analyze_NoExclusions_IdenticalToTwoArgOverload()
    {
        var docMap = BuildMap("docs/login.md", "docs/billing.md", "docs/admin.md");
        var tests = new List<TestCase> { CreateTestCase("TC-001", ["docs/login.md"]) };

        var baseline = _analyzer.Analyze(docMap, tests);
        var withEmpty = _analyzer.Analyze(docMap, tests, []);
        var withNull = _analyzer.Analyze(docMap, tests, null);

        foreach (var result in new[] { withEmpty, withNull })
        {
            Assert.Equal(baseline.TotalDocs, result.TotalDocs);
            Assert.Equal(baseline.CoveredDocs, result.CoveredDocs);
            Assert.Equal(baseline.Percentage, result.Percentage);
            Assert.Equal(baseline.Details.Count, result.Details.Count);
            Assert.Equal(0, result.ExcludedDocs);
            Assert.All(result.Details, d => Assert.False(d.Excluded));
        }
    }

    [Fact]
    public void Serialize_NoExclusions_OmitsNewKeys()
    {
        // WhenWritingDefault guarantees byte-for-byte unchanged JSON (FR-005).
        var docMap = BuildMap("docs/login.md");
        var result = _analyzer.Analyze(docMap, [CreateTestCase("TC-001", ["docs/login.md"])]);

        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("excluded_docs", json);
        Assert.DoesNotContain("\"excluded\"", json);
        Assert.DoesNotContain("excluded_by_pattern", json);
    }

    [Fact]
    public void Serialize_WithExclusions_EmitsNewKeys()
    {
        var docMap = BuildMap("docs/login.md", "docs/release-notes/v1.md");
        var result = _analyzer.Analyze(docMap, [], ["docs/release-notes/**"]);

        var json = JsonSerializer.Serialize(result);

        Assert.Contains("excluded_docs", json);
        Assert.Contains("excluded_by_pattern", json);
    }

    // ---- US4: three-concept disambiguation (FR-006, SC-005) ----

    [Fact]
    public void Analyze_DocMatchedByAnalysisPatternsOnly_StillCountsInDenominator()
    {
        // A doc that would match coverage.analysis_exclude_patterns (e.g.
        // **/release-notes/**) is NOT excluded from coverage unless it is in the
        // SEPARATE coverage_exclude_patterns list. Here we pass NO coverage
        // exclusions — the doc must still count.
        var docMap = BuildMap("docs/login.md", "docs/release-notes/v1.md");
        var tests = new List<TestCase> { CreateTestCase("TC-001", ["docs/login.md"]) };

        var result = _analyzer.Analyze(docMap, tests, coverageExcludePatterns: []);

        Assert.Equal(2, result.TotalDocs); // release-notes still in denominator
        Assert.Equal(0, result.ExcludedDocs);
        Assert.Equal(50m, result.Percentage);

        // Same doc, once added to the coverage-scoped list, becomes excluded.
        var excludedResult = _analyzer.Analyze(docMap, tests, ["docs/release-notes/**"]);
        Assert.Equal(1, excludedResult.TotalDocs);
        Assert.Equal(1, excludedResult.ExcludedDocs);
        Assert.Equal(100m, excludedResult.Percentage);
    }

    private static DocumentMap BuildMap(params string[] paths) => new()
    {
        TotalSizeKb = paths.Length * 5,
        Documents = paths
            .Select(p => new DocumentEntry
            {
                Path = p,
                Title = p,
                SizeKb = 5,
                Headings = [p],
                Preview = ""
            })
            .ToList()
    };

    private static TestCase CreateTestCase(string id, IReadOnlyList<string> sourceRefs) => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Priority = Priority.Medium,
        Title = $"Test {id}",
        ExpectedResult = "Expected",
        SourceRefs = sourceRefs
    };
}
