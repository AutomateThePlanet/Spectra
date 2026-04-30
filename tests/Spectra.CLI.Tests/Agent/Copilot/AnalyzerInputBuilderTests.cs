using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Index;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Tests.Agent.Copilot;

/// <summary>
/// Tests the Phase-4 bridge between the manifest layout and the existing
/// <see cref="BehaviorAnalyzer"/> input. Verifies suite filtering, pre-flight
/// budget enforcement, and graceful degradation when the manifest is absent.
/// </summary>
public class AnalyzerInputBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public AnalyzerInputBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-aib-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private static SourceDocument Doc(string path, string content = "Sample.") =>
        new()
        {
            Path = path,
            Title = Path.GetFileNameWithoutExtension(path),
            Content = content,
            Sections = new List<string>(),
            SizeKb = 1,
        };

    /// <summary>Writes a minimal valid manifest+suite-files layout for tests.</summary>
    private async Task SeedManifestAsync(params (string SuiteId, string[] DocPaths, bool Skip)[] suites)
    {
        var indexDir = Path.Combine(_tempDir, "docs", "_index");
        Directory.CreateDirectory(Path.Combine(indexDir, "groups"));

        var groups = new List<DocSuiteEntry>();
        foreach (var (suiteId, docPaths, skip) in suites)
        {
            groups.Add(new DocSuiteEntry
            {
                Id = suiteId,
                Title = suiteId,
                Path = $"docs/{suiteId}",
                DocumentCount = docPaths.Length,
                TokensEstimated = docPaths.Length * 100,
                SkipAnalysis = skip,
                ExcludedBy = skip ? "pattern" : "none",
                ExcludedPattern = skip ? "**/Old/**" : null,
                IndexFile = $"groups/{suiteId}.index.md",
            });

            var suiteFile = new SuiteIndexFile
            {
                SuiteId = suiteId,
                GeneratedAt = DateTimeOffset.UtcNow,
                DocumentCount = docPaths.Length,
                TokensEstimated = docPaths.Length * 100,
                Entries = docPaths.Select(p => new DocumentIndexEntry
                {
                    Path = p,
                    Title = Path.GetFileNameWithoutExtension(p),
                    Sections = new List<SectionSummary>(),
                    KeyEntities = new List<string>(),
                    WordCount = 50,
                    EstimatedTokens = 100,
                    SizeKb = 1,
                    LastModified = DateTimeOffset.UtcNow,
                    ContentHash = "",
                }).ToList(),
            };

            await new SuiteIndexFileWriter().WriteAsync(
                Path.Combine(indexDir, "groups", $"{suiteId}.index.md"),
                suiteFile);
        }

        var manifest = new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalDocuments = groups.Sum(g => g.DocumentCount),
            TotalWords = 0,
            TotalTokensEstimated = groups.Sum(g => g.TokensEstimated),
            Groups = groups,
        };

        await new DocIndexManifestWriter().WriteAsync(
            Path.Combine(indexDir, "_manifest.yaml"), manifest);
    }

    [Fact]
    public async Task BuildAsync_WithSuiteFilter_FiltersDocumentsToThatSuite()
    {
        await SeedManifestAsync(
            ("checkout", new[] { "docs/checkout/a.md", "docs/checkout/b.md" }, false),
            ("payments", new[] { "docs/payments/x.md" }, false));

        var allDocs = new List<SourceDocument>
        {
            Doc("docs/checkout/a.md"),
            Doc("docs/checkout/b.md"),
            Doc("docs/payments/x.md"),
        };

        var builder = new AnalyzerInputBuilder();
        var result = await builder.BuildAsync(
            basePath: _tempDir,
            manifestPath: Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml"),
            indexDir: Path.Combine(_tempDir, "docs", "_index"),
            allDocuments: allDocs,
            suiteFilter: "checkout",
            focusFilter: null,
            budgetTokens: 96_000,
            includeArchived: false);

        Assert.Equal(2, result.FilteredDocuments.Count);
        Assert.All(result.FilteredDocuments, d => Assert.StartsWith("docs/checkout/", d.Path));
    }

    [Fact]
    public async Task BuildAsync_NoFilter_LoadsAllNonArchivedSuites()
    {
        await SeedManifestAsync(
            ("checkout", new[] { "docs/checkout/a.md" }, false),
            ("Old", new[] { "docs/Old/x.md" }, true));

        var allDocs = new List<SourceDocument>
        {
            Doc("docs/checkout/a.md"),
            Doc("docs/Old/x.md"),
        };

        var builder = new AnalyzerInputBuilder();
        var result = await builder.BuildAsync(
            basePath: _tempDir,
            manifestPath: Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml"),
            indexDir: Path.Combine(_tempDir, "docs", "_index"),
            allDocuments: allDocs,
            suiteFilter: null,
            focusFilter: null,
            budgetTokens: 96_000,
            includeArchived: false);

        Assert.Single(result.FilteredDocuments);
        Assert.Equal("docs/checkout/a.md", result.FilteredDocuments[0].Path);
    }

    [Fact]
    public async Task BuildAsync_IncludeArchivedTrue_LoadsSkipSuitesToo()
    {
        await SeedManifestAsync(
            ("checkout", new[] { "docs/checkout/a.md" }, false),
            ("Old", new[] { "docs/Old/x.md" }, true));

        var allDocs = new List<SourceDocument>
        {
            Doc("docs/checkout/a.md"),
            Doc("docs/Old/x.md"),
        };

        var builder = new AnalyzerInputBuilder();
        var result = await builder.BuildAsync(
            basePath: _tempDir,
            manifestPath: Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml"),
            indexDir: Path.Combine(_tempDir, "docs", "_index"),
            allDocuments: allDocs,
            suiteFilter: null,
            focusFilter: null,
            budgetTokens: 96_000,
            includeArchived: true);

        Assert.Equal(2, result.FilteredDocuments.Count);
    }

    [Fact]
    public async Task BuildAsync_BudgetExceeded_ThrowsActionableException()
    {
        // 200 docs × 550 tokens (full 2KB content) ≈ 110K + 8K overhead → exceeds 96K.
        var docs = Enumerable.Range(1, 200)
            .Select(i => $"docs/many/{i}.md")
            .ToArray();
        await SeedManifestAsync(("many", docs, false));

        // Each doc has 2KB of content so the estimator hits the per-doc cap.
        var docContent = new string('x', 2000);
        var allDocs = docs.Select(p => Doc(p, content: docContent)).Cast<SourceDocument>().ToList();

        var builder = new AnalyzerInputBuilder();

        var ex = await Assert.ThrowsAsync<PreFlightBudgetExceededException>(() =>
            builder.BuildAsync(
                basePath: _tempDir,
                manifestPath: Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml"),
                indexDir: Path.Combine(_tempDir, "docs", "_index"),
                allDocuments: allDocs,
                suiteFilter: null,
                focusFilter: null,
                budgetTokens: 96_000,
                includeArchived: false));

        Assert.True(ex.EstimatedTokens > 96_000);
        Assert.Contains("many", ex.Message);
        Assert.Contains("--analyze-only", ex.Message);
        Assert.Contains("ai.analysis.max_prompt_tokens", ex.Message);
    }

    [Fact]
    public async Task BuildAsync_UnknownSuiteFilter_WarnsAndFallsBack()
    {
        await SeedManifestAsync(
            ("checkout", new[] { "docs/checkout/a.md" }, false));

        var allDocs = new List<SourceDocument>
        {
            Doc("docs/checkout/a.md"),
        };

        var builder = new AnalyzerInputBuilder();
        var result = await builder.BuildAsync(
            basePath: _tempDir,
            manifestPath: Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml"),
            indexDir: Path.Combine(_tempDir, "docs", "_index"),
            allDocuments: allDocs,
            suiteFilter: "nonexistent",
            focusFilter: null,
            budgetTokens: 96_000,
            includeArchived: false);

        Assert.Single(result.FilteredDocuments);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("nonexistent", result.Warnings[0]);
    }

    [Fact]
    public async Task BuildAsync_NoManifest_PassesThroughWithoutFiltering()
    {
        var allDocs = new List<SourceDocument>
        {
            Doc("docs/a.md"),
            Doc("docs/b.md"),
        };

        var builder = new AnalyzerInputBuilder();
        var result = await builder.BuildAsync(
            basePath: _tempDir,
            manifestPath: Path.Combine(_tempDir, "nonexistent_manifest.yaml"),
            indexDir: Path.Combine(_tempDir, "nonexistent"),
            allDocuments: allDocs,
            suiteFilter: "anything",
            focusFilter: null,
            budgetTokens: 96_000,
            includeArchived: false);

        Assert.Equal(2, result.FilteredDocuments.Count);
        Assert.Empty(result.SelectedSuites);
    }

    [Fact]
    public void EstimatePromptTokens_ScalesLinearlyWithDocCount()
    {
        var twoDocs = new List<SourceDocument> { Doc("docs/a.md"), Doc("docs/b.md") };
        var fourDocs = twoDocs.Concat(twoDocs).ToList();

        var two = AnalyzerInputBuilder.EstimatePromptTokens(twoDocs);
        var four = AnalyzerInputBuilder.EstimatePromptTokens(fourDocs);

        // Overhead is constant; per-doc cost doubles.
        Assert.True(four - two == two - AnalyzerInputBuilder.PromptOverheadTokens);
    }

    [Fact]
    public void EstimatePromptTokens_TinyDocsCostFarLessThanCap()
    {
        var tiny = new List<SourceDocument> { Doc("docs/a.md", content: "x") };

        var estimate = AnalyzerInputBuilder.EstimatePromptTokens(tiny);

        // Worst-case (cap-bound) estimate would be ~550 tokens/doc + overhead.
        // Actual estimate for a 1-char doc should be far smaller — proves the
        // estimator looks at real content length, not the cap.
        var worstCase = AnalyzerInputBuilder.PromptOverheadTokens
                        + AnalyzerInputBuilder.PerDocumentTokenEstimate;
        Assert.True(estimate < worstCase,
            $"Tiny doc estimated at {estimate} tokens but worst-case is {worstCase} — estimator is still pessimistic.");
    }

    [Fact]
    public void EstimatePromptTokens_LargeDocsCappedAtPerDocLimit()
    {
        var huge = new List<SourceDocument>
        {
            Doc("docs/a.md", content: new string('x', 10_000)),
        };

        var estimate = AnalyzerInputBuilder.EstimatePromptTokens(huge);

        // Even a 10K-char document caps at the 2000-char per-doc preview limit
        // mirrored from BehaviorAnalyzer.FormatDocuments.
        Assert.Equal(
            AnalyzerInputBuilder.PromptOverheadTokens + AnalyzerInputBuilder.PerDocumentTokenEstimate,
            estimate);
    }
}
