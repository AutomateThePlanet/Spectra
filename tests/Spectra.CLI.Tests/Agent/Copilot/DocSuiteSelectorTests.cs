using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Tests.Agent.Copilot;

public class DocSuiteSelectorTests
{
    private static DocIndexManifest BuildManifest(params (string Id, int Tokens, bool Skip)[] entries)
    {
        var groups = entries.Select(e => new DocSuiteEntry
        {
            Id = e.Id,
            Title = e.Id,
            Path = $"docs/{e.Id}",
            DocumentCount = 1,
            TokensEstimated = e.Tokens,
            SkipAnalysis = e.Skip,
            ExcludedBy = e.Skip ? "pattern" : "none",
            ExcludedPattern = e.Skip ? "**/Old/**" : null,
            IndexFile = $"groups/{e.Id}.index.md",
        }).ToList();

        return new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalDocuments = groups.Count,
            TotalWords = 0,
            TotalTokensEstimated = groups.Sum(g => g.TokensEstimated),
            Groups = groups,
        };
    }

    [Fact]
    public void Select_ExactSuiteMatch_ReturnsOnlyThatSuite()
    {
        var manifest = BuildManifest(
            ("checkout", 5000, false),
            ("payments", 3000, false));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: "checkout", focusFilter: null, budgetTokens: 96_000);

        Assert.Single(result.Selected);
        Assert.Equal("checkout", result.Selected[0].Id);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Select_UnknownSuiteFilter_WarnsAndFallsBackToNoFilter()
    {
        var manifest = BuildManifest(
            ("checkout", 5000, false),
            ("payments", 3000, false));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: "missing", focusFilter: null, budgetTokens: 96_000);

        Assert.Equal(2, result.Selected.Count);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("missing", result.Warnings[0]);
        Assert.Contains("checkout", result.Warnings[0]);
    }

    [Fact]
    public void Select_NoFilter_SkipsArchivedSuitesByDefault()
    {
        var manifest = BuildManifest(
            ("checkout", 5000, false),
            ("Old", 2000, true));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: null, budgetTokens: 96_000);

        Assert.Single(result.Selected);
        Assert.Equal("checkout", result.Selected[0].Id);
    }

    [Fact]
    public void Select_NoFilter_IncludeArchivedTrue_IncludesSkipSuites()
    {
        var manifest = BuildManifest(
            ("checkout", 5000, false),
            ("Old", 2000, true));
        var selector = new DocSuiteSelector();

        var result = selector.Select(
            manifest, suiteFilter: null, focusFilter: null, budgetTokens: 96_000, includeArchived: true);

        Assert.Equal(2, result.Selected.Count);
    }

    [Fact]
    public void Select_NoFilter_OrdersByTokensDescending()
    {
        var manifest = BuildManifest(
            ("small", 1000, false),
            ("large", 50_000, false),
            ("medium", 10_000, false));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: null, budgetTokens: 96_000);

        Assert.Equal("large", result.Selected[0].Id);
        Assert.Equal("medium", result.Selected[1].Id);
        Assert.Equal("small", result.Selected[2].Id);
    }

    [Fact]
    public void Select_FocusFilter_PicksMatchingSuiteByKeyword()
    {
        var manifest = BuildManifest(
            ("checkout", 5000, false),
            ("payments", 3000, false));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: "payment", budgetTokens: 96_000);

        Assert.Single(result.Selected);
        Assert.Equal("payments", result.Selected[0].Id);
    }

    [Fact]
    public void Select_FocusFilter_NoMatch_FallsBackToAllNonArchived()
    {
        var manifest = BuildManifest(
            ("checkout", 5000, false),
            ("payments", 3000, false));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: "shipping", budgetTokens: 96_000);

        Assert.Equal(2, result.Selected.Count);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Select_FocusFilter_PacksToBudgetTarget()
    {
        // Three suites match "test"; budget 10K means 70% target = 7K tokens.
        // Should pick first suite (5K) only — adding the next (4K) would exceed 7K.
        var manifest = BuildManifest(
            ("test_first", 5_000, false),
            ("test_second", 4_000, false),
            ("test_third", 3_000, false));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: "test", budgetTokens: 10_000);

        Assert.Single(result.Selected);
        Assert.Equal("test_first", result.Selected[0].Id);
    }

    [Fact]
    public void Select_EmptyManifest_ReturnsEmpty()
    {
        var manifest = new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalDocuments = 0,
            TotalWords = 0,
            TotalTokensEstimated = 0,
            Groups = new List<DocSuiteEntry>(),
        };
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: null, budgetTokens: 96_000);

        Assert.Empty(result.Selected);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Select_AllSuitesArchivedAndIncludeArchivedFalse_ReturnsEmpty()
    {
        var manifest = BuildManifest(
            ("Old", 2000, true),
            ("legacy", 3000, true));
        var selector = new DocSuiteSelector();

        var result = selector.Select(manifest, suiteFilter: null, focusFilter: null, budgetTokens: 96_000);

        Assert.Empty(result.Selected);
    }
}
