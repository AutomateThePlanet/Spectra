using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.Core.Tests.Coverage;

public class DocumentationCoverageAnalyzerTests
{
    private readonly DocumentationCoverageAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_AllDocsCovered_Returns100Percent()
    {
        var docMap = new DocumentMap
        {
            TotalSizeKb = 10,
            Documents =
            [
                new DocumentEntry { Path = "docs/auth.md", Title = "Auth", SizeKb = 5, Headings = ["Auth"], Preview = "" },
                new DocumentEntry { Path = "docs/billing.md", Title = "Billing", SizeKb = 5, Headings = ["Billing"], Preview = "" }
            ]
        };

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["docs/auth.md"]),
            CreateTestCase("TC-002", ["docs/billing.md"])
        };

        var result = _analyzer.Analyze(docMap, tests);

        Assert.Equal(2, result.TotalDocs);
        Assert.Equal(2, result.CoveredDocs);
        Assert.Equal(100m, result.Percentage);
        Assert.All(result.Details, d => Assert.True(d.Covered));
    }

    [Fact]
    public void Analyze_NoCoverage_Returns0Percent()
    {
        var docMap = new DocumentMap
        {
            TotalSizeKb = 10,
            Documents =
            [
                new DocumentEntry { Path = "docs/auth.md", Title = "Auth", SizeKb = 5, Headings = ["Auth"], Preview = "" }
            ]
        };

        var result = _analyzer.Analyze(docMap, []);

        Assert.Equal(1, result.TotalDocs);
        Assert.Equal(0, result.CoveredDocs);
        Assert.Equal(0m, result.Percentage);
    }

    [Fact]
    public void Analyze_PartialCoverage_ReturnsCorrectPercentage()
    {
        var docMap = new DocumentMap
        {
            TotalSizeKb = 15,
            Documents =
            [
                new DocumentEntry { Path = "docs/auth.md", Title = "Auth", SizeKb = 5, Headings = ["Auth"], Preview = "" },
                new DocumentEntry { Path = "docs/billing.md", Title = "Billing", SizeKb = 5, Headings = ["Billing"], Preview = "" },
                new DocumentEntry { Path = "docs/admin.md", Title = "Admin", SizeKb = 5, Headings = ["Admin"], Preview = "" }
            ]
        };

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["docs/auth.md"])
        };

        var result = _analyzer.Analyze(docMap, tests);

        Assert.Equal(3, result.TotalDocs);
        Assert.Equal(1, result.CoveredDocs);
        Assert.Equal(33.33m, result.Percentage);
    }

    [Fact]
    public void Analyze_NoDocs_Returns0Percent()
    {
        var docMap = new DocumentMap { TotalSizeKb = 0, Documents = [] };

        var result = _analyzer.Analyze(docMap, []);

        Assert.Equal(0, result.TotalDocs);
        Assert.Equal(0m, result.Percentage);
    }

    [Fact]
    public void Analyze_MultipleTestsPerDoc_CountsCorrectly()
    {
        var docMap = new DocumentMap
        {
            TotalSizeKb = 5,
            Documents =
            [
                new DocumentEntry { Path = "docs/auth.md", Title = "Auth", SizeKb = 5, Headings = ["Auth"], Preview = "" }
            ]
        };

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["docs/auth.md"]),
            CreateTestCase("TC-002", ["docs/auth.md"]),
            CreateTestCase("TC-003", ["docs/auth.md"])
        };

        var result = _analyzer.Analyze(docMap, tests);

        Assert.Single(result.Details);
        Assert.Equal(3, result.Details[0].TestCount);
        Assert.Equal(3, result.Details[0].TestIds.Count);
    }

    [Fact]
    public void Analyze_SourceRefsWithFragmentAnchors_MatchesDocPath()
    {
        var docMap = new DocumentMap
        {
            TotalSizeKb = 10,
            Documents =
            [
                new DocumentEntry { Path = "docs/auth.md", Title = "Auth", SizeKb = 5, Headings = ["Auth"], Preview = "" },
                new DocumentEntry { Path = "docs/billing.md", Title = "Billing", SizeKb = 5, Headings = ["Billing"], Preview = "" }
            ]
        };

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["docs/auth.md#Login-Flow"]),
            CreateTestCase("TC-002", ["docs/auth.md#Session-Management", "docs/billing.md#Refunds"])
        };

        var result = _analyzer.Analyze(docMap, tests);

        Assert.Equal(2, result.TotalDocs);
        Assert.Equal(2, result.CoveredDocs);
        Assert.Equal(100m, result.Percentage);

        var authDetail = result.Details.First(d => d.Doc == "docs/auth.md");
        Assert.Equal(2, authDetail.TestCount);
        Assert.Contains("TC-001", authDetail.TestIds);
        Assert.Contains("TC-002", authDetail.TestIds);

        var billingDetail = result.Details.First(d => d.Doc == "docs/billing.md");
        Assert.Equal(1, billingDetail.TestCount);
        Assert.Contains("TC-002", billingDetail.TestIds);
    }

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
