using Spectra.CLI.Coverage;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Coverage;

public class GapAnalyzerTests
{
    [Fact]
    public void AnalyzeGaps_SourceRefsWithFragments_MatchesDocPaths()
    {
        var documentMap = new DocumentMap
        {
            TotalSizeKb = 5,
            Documents =
            [
                new DocumentEntry
                {
                    Path = "docs/checkout.md",
                    Title = "Checkout",
                    Headings = ["Payment Flow", "Cart"],
                    Preview = "Checkout documentation",
                    SizeKb = 5
                }
            ]
        };

        var tests = new List<TestCase>
        {
            new()
            {
                Id = "TC-001",
                Title = "Payment Flow",
                FilePath = "checkout/TC-001.md",
                ExpectedResult = "Payment completes",
                Priority = Priority.High,
                Steps = [],
                SourceRefs = ["docs/checkout.md#Payment-Flow"]
            }
        };

        var analyzer = new GapAnalyzer();
        var gaps = analyzer.AnalyzeGaps(documentMap, tests);

        // No document-level gap for checkout.md since fragment-anchored ref matches
        Assert.DoesNotContain(gaps, g => g.Section is null && g.DocumentPath == "docs/checkout.md");
    }

    [Fact]
    public void AnalyzeGaps_SourceRefsWithoutFragments_StillMatches()
    {
        var documentMap = new DocumentMap
        {
            TotalSizeKb = 5,
            Documents =
            [
                new DocumentEntry
                {
                    Path = "docs/checkout.md",
                    Title = "Checkout",
                    Headings = ["Payment Flow"],
                    Preview = "Checkout documentation",
                    SizeKb = 5
                }
            ]
        };

        var tests = new List<TestCase>
        {
            new()
            {
                Id = "TC-001",
                Title = "Payment Flow",
                FilePath = "checkout/TC-001.md",
                ExpectedResult = "Payment completes",
                Priority = Priority.High,
                Steps = [],
                SourceRefs = ["docs/checkout.md"]
            }
        };

        var analyzer = new GapAnalyzer();
        var gaps = analyzer.AnalyzeGaps(documentMap, tests);

        Assert.DoesNotContain(gaps, g => g.Section is null && g.DocumentPath == "docs/checkout.md");
    }

    [Fact]
    public void AnalyzeGaps_MultipleFragmentsSameDoc_DoesNotCreateDocGap()
    {
        var documentMap = new DocumentMap
        {
            TotalSizeKb = 5,
            Documents =
            [
                new DocumentEntry
                {
                    Path = "docs/checkout.md",
                    Title = "Checkout",
                    Headings = ["Payment", "Cart", "Shipping"],
                    Preview = "Checkout documentation",
                    SizeKb = 10
                }
            ]
        };

        var tests = new List<TestCase>
        {
            new()
            {
                Id = "TC-001", Title = "Payment", FilePath = "checkout/TC-001.md", ExpectedResult = "OK",
                Priority = Priority.High, Steps = [],
                SourceRefs = ["docs/checkout.md#Payment"]
            },
            new()
            {
                Id = "TC-002", Title = "Cart", FilePath = "checkout/TC-002.md", ExpectedResult = "OK",
                Priority = Priority.High, Steps = [],
                SourceRefs = ["docs/checkout.md#Cart"]
            }
        };

        var analyzer = new GapAnalyzer();
        var gaps = analyzer.AnalyzeGaps(documentMap, tests);

        // Both refs resolve to same doc — no document-level gap
        Assert.DoesNotContain(gaps, g => g.Section is null && g.DocumentPath == "docs/checkout.md");
    }

    [Fact]
    public void GetRemainingGaps_WithFragmentRefs_FiltersCorrectly()
    {
        var originalGaps = new List<CoverageGap>
        {
            new()
            {
                DocumentPath = "docs/checkout.md",
                Section = null,
                Reason = "No tests reference this document",
                Severity = GapSeverity.Medium
            }
        };

        var newTests = new List<TestCase>
        {
            new()
            {
                Id = "TC-001", Title = "Payment", FilePath = "checkout/TC-001.md", ExpectedResult = "OK",
                Priority = Priority.High, Steps = [],
                SourceRefs = ["docs/checkout.md#Payment-Flow"]
            }
        };

        var analyzer = new GapAnalyzer();
        var remaining = analyzer.GetRemainingGaps(originalGaps, newTests);

        // Fragment-anchored ref should cover the doc gap
        Assert.Empty(remaining);
    }
}
