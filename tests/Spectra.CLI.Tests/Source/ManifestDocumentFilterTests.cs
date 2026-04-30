using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Tests.Source;

public class ManifestDocumentFilterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _indexDir;
    private readonly string _manifestPath;

    public ManifestDocumentFilterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-mdf-test-{Guid.NewGuid():N}");
        _indexDir = Path.Combine(_tempDir, "docs", "_index");
        _manifestPath = Path.Combine(_indexDir, "_manifest.yaml");
        Directory.CreateDirectory(Path.Combine(_indexDir, "groups"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private async Task SeedAsync(params (string SuiteId, string[] Docs, bool Skip)[] suites)
    {
        var groups = new List<DocSuiteEntry>();
        foreach (var (id, paths, skip) in suites)
        {
            groups.Add(new DocSuiteEntry
            {
                Id = id,
                Title = id,
                Path = $"docs/{id}",
                DocumentCount = paths.Length,
                TokensEstimated = paths.Length * 100,
                SkipAnalysis = skip,
                ExcludedBy = skip ? "pattern" : "none",
                ExcludedPattern = skip ? "**/Old/**" : null,
                IndexFile = $"groups/{id}.index.md",
            });

            await new SuiteIndexFileWriter().WriteAsync(
                Path.Combine(_indexDir, "groups", $"{id}.index.md"),
                new SuiteIndexFile
                {
                    SuiteId = id,
                    GeneratedAt = DateTimeOffset.UtcNow,
                    DocumentCount = paths.Length,
                    TokensEstimated = paths.Length * 100,
                    Entries = paths.Select(p => new DocumentIndexEntry
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
                });
        }

        await new DocIndexManifestWriter().WriteAsync(_manifestPath, new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalDocuments = groups.Sum(g => g.DocumentCount),
            TotalWords = 0,
            TotalTokensEstimated = groups.Sum(g => g.TokensEstimated),
            Groups = groups,
        });
    }

    private static DocumentMap BuildMap(params string[] paths) => new()
    {
        Documents = paths.Select(p => new DocumentEntry
        {
            Path = p,
            Title = Path.GetFileNameWithoutExtension(p),
            SizeKb = 1,
            Headings = new List<string>(),
            Preview = "",
        }).ToList(),
        TotalSizeKb = paths.Length,
    };

    [Fact]
    public async Task FilterAsync_DropsSkipAnalysisDocuments()
    {
        await SeedAsync(
            ("active", new[] { "docs/active/foo.md" }, false),
            ("Old", new[] { "docs/Old/legacy.md" }, true));
        var map = BuildMap("docs/active/foo.md", "docs/Old/legacy.md");

        var filtered = await ManifestDocumentFilter.FilterAsync(
            map, _manifestPath, _indexDir, includeArchived: false);

        Assert.Single(filtered.Documents);
        Assert.Equal("docs/active/foo.md", filtered.Documents[0].Path);
    }

    [Fact]
    public async Task FilterAsync_IncludeArchivedTrue_PassesThrough()
    {
        await SeedAsync(
            ("active", new[] { "docs/active/foo.md" }, false),
            ("Old", new[] { "docs/Old/legacy.md" }, true));
        var map = BuildMap("docs/active/foo.md", "docs/Old/legacy.md");

        var filtered = await ManifestDocumentFilter.FilterAsync(
            map, _manifestPath, _indexDir, includeArchived: true);

        Assert.Equal(2, filtered.Documents.Count);
    }

    [Fact]
    public async Task FilterAsync_NoManifest_PassesThroughUnchanged()
    {
        var map = BuildMap("docs/foo.md", "docs/bar.md");

        var filtered = await ManifestDocumentFilter.FilterAsync(
            map,
            Path.Combine(_tempDir, "nonexistent.yaml"),
            _indexDir,
            includeArchived: false);

        Assert.Equal(2, filtered.Documents.Count);
    }

    [Fact]
    public async Task FilterAsync_NoSkipSuites_ReturnsInputUnchanged()
    {
        await SeedAsync(("active", new[] { "docs/active/a.md", "docs/active/b.md" }, false));
        var map = BuildMap("docs/active/a.md", "docs/active/b.md");

        var filtered = await ManifestDocumentFilter.FilterAsync(
            map, _manifestPath, _indexDir, includeArchived: false);

        Assert.Equal(2, filtered.Documents.Count);
    }

    [Fact]
    public async Task FilterAsync_RecomputesTotalSizeKb()
    {
        await SeedAsync(
            ("active", new[] { "docs/active/foo.md" }, false),
            ("Old", new[] { "docs/Old/legacy.md" }, true));
        var map = new DocumentMap
        {
            Documents = new List<DocumentEntry>
            {
                new() { Path = "docs/active/foo.md", Title = "Active", SizeKb = 5, Headings = new List<string>(), Preview = "" },
                new() { Path = "docs/Old/legacy.md", Title = "Legacy", SizeKb = 7, Headings = new List<string>(), Preview = "" },
            },
            TotalSizeKb = 12,
        };

        var filtered = await ManifestDocumentFilter.FilterAsync(
            map, _manifestPath, _indexDir, includeArchived: false);

        Assert.Equal(5, filtered.TotalSizeKb);
    }
}
