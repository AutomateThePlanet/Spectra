using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Index;

namespace Spectra.Core.Tests.Index;

public class SuiteIndexFileRoundTripTests
{
    [Fact]
    public void RoundTrip_PreservesEntryShape()
    {
        var original = new SuiteIndexFile
        {
            SuiteId = "checkout",
            GeneratedAt = new DateTimeOffset(2026, 4, 29, 15, 0, 0, TimeSpan.Zero),
            DocumentCount = 1,
            TokensEstimated = 130,
            Entries = new List<DocumentIndexEntry>
            {
                new()
                {
                    Path = "docs/checkout/process.md",
                    Title = "Checkout process",
                    Sections = new List<SectionSummary>
                    {
                        new() { Heading = "Overview", Level = 2, Summary = "How checkout works." },
                    },
                    KeyEntities = new List<string> { "Cart", "Payment" },
                    WordCount = 100,
                    EstimatedTokens = 130,
                    SizeKb = 2,
                    LastModified = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                    ContentHash = "",
                },
            },
        };

        var rendered = SuiteIndexFileWriter.Render(original);
        var roundtrip = SuiteIndexFileReader.Parse(rendered, "checkout");

        Assert.Equal(1, roundtrip.DocumentCount);
        Assert.Single(roundtrip.Entries);
        var entry = roundtrip.Entries[0];
        Assert.Equal("docs/checkout/process.md", entry.Path);
        Assert.Equal("Checkout process", entry.Title);
        Assert.Equal(100, entry.WordCount);
        Assert.Equal(130, entry.EstimatedTokens);
        Assert.Equal(2, entry.SizeKb);
        Assert.Contains("Cart", entry.KeyEntities);
        Assert.Single(entry.Sections);
        Assert.Equal("Overview", entry.Sections[0].Heading);
    }

    [Fact]
    public void RoundTrip_EscapesPipesInSummaries()
    {
        var original = new SuiteIndexFile
        {
            SuiteId = "test",
            GeneratedAt = DateTimeOffset.UtcNow,
            DocumentCount = 1,
            TokensEstimated = 50,
            Entries = new List<DocumentIndexEntry>
            {
                new()
                {
                    Path = "docs/test/x.md",
                    Title = "Pipe heavy",
                    Sections = new List<SectionSummary>
                    {
                        new() { Heading = "Body", Level = 2, Summary = "Use | pipes | sparingly." },
                    },
                    KeyEntities = new List<string>(),
                    WordCount = 10,
                    EstimatedTokens = 13,
                    SizeKb = 1,
                    LastModified = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                    ContentHash = "",
                },
            },
        };

        var rendered = SuiteIndexFileWriter.Render(original);
        var roundtrip = SuiteIndexFileReader.Parse(rendered, "test");

        Assert.Equal("Use | pipes | sparingly.", roundtrip.Entries[0].Sections[0].Summary);
    }

    [Fact]
    public void Render_NoSectionsTable_WhenEntryHasNoSections()
    {
        var file = new SuiteIndexFile
        {
            SuiteId = "noheaders",
            GeneratedAt = DateTimeOffset.UtcNow,
            DocumentCount = 1,
            TokensEstimated = 40,
            Entries = new List<DocumentIndexEntry>
            {
                new()
                {
                    Path = "docs/noheaders/x.md",
                    Title = "Plain",
                    Sections = new List<SectionSummary>(),
                    KeyEntities = new List<string>(),
                    WordCount = 30,
                    EstimatedTokens = 40,
                    SizeKb = 1,
                    LastModified = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                    ContentHash = "",
                },
            },
        };

        var rendered = SuiteIndexFileWriter.Render(file);

        Assert.DoesNotContain("| Section |", rendered);
    }
}
