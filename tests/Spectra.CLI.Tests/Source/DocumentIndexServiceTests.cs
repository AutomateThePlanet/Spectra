using Spectra.CLI.Source;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Source;

public class DocumentIndexServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DocumentIndexService _service;

    public DocumentIndexServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spectra-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new DocumentIndexService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private static (SourceConfig Source, CoverageConfig Coverage) Configs() =>
        (new SourceConfig { LocalDir = "docs/" }, new CoverageConfig());

    [Fact]
    public async Task EnsureNewLayoutAsync_CreatesManifestFromDocs()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), "# Test Doc\n\n## Section One\n\nSome content here.");

        var (source, coverage) = Configs();
        var result = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: true);

        Assert.Equal(1, result.Manifest.TotalDocuments);
        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.ChecksumsPath));
    }

    [Fact]
    public async Task EnsureNewLayoutAsync_IncrementalSkipsUnchangedFiles()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), "# Doc A\n\nContent A.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "b.md"), "# Doc B\n\nContent B.");

        var (source, coverage) = Configs();

        var first = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: true);
        Assert.Equal(2, first.Manifest.TotalDocuments);

        var second = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: false);
        Assert.Equal(2, second.Manifest.TotalDocuments);
        Assert.Equal(0, second.NewDocuments);
        Assert.Equal(0, second.ChangedDocuments);

        // Modify one file
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), "# Doc A Updated\n\nNew content here.");

        var third = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: false);
        Assert.Equal(2, third.Manifest.TotalDocuments);
        Assert.Equal(1, third.ChangedDocuments);
    }

    [Fact]
    public async Task EnsureNewLayoutAsync_HandlesNoDocs()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);

        var (source, coverage) = Configs();
        var result = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: true);

        Assert.Equal(0, result.Manifest.TotalDocuments);
    }

    [Fact]
    public async Task EnsureNewLayoutAsync_ForceRebuildRewritesAll()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), "# Test\n\nContent.");

        var (source, coverage) = Configs();
        await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: false);
        var result = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: true);

        Assert.Equal(1, result.Manifest.TotalDocuments);
        // Force rebuild treats every file as new (no reuse).
        Assert.Equal(1, result.NewDocuments);
    }

    [Fact]
    public async Task EnsureNewLayoutAsync_HandlesRemovedFiles()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        var filePath = Path.Combine(docsDir, "temp.md");
        await File.WriteAllTextAsync(filePath, "# Temp\n\nContent.");

        var (source, coverage) = Configs();
        var first = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: true);
        Assert.Equal(1, first.Manifest.TotalDocuments);

        File.Delete(filePath);
        var second = await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: false);
        Assert.Equal(0, second.Manifest.TotalDocuments);
    }

    [Fact]
    public void ToDocumentMap_ProjectsCorrectly()
    {
        var index = new Core.Models.DocumentIndex
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalWordCount = 100,
            TotalEstimatedTokens = 130,
            Entries =
            [
                new Core.Models.DocumentIndexEntry
                {
                    Path = "docs/test.md",
                    Title = "Test",
                    Sections =
                    [
                        new Core.Models.SectionSummary { Heading = "Overview", Level = 2, Summary = "First section" },
                        new Core.Models.SectionSummary { Heading = "Details", Level = 3, Summary = "Details section" }
                    ],
                    KeyEntities = ["Entity1"],
                    WordCount = 100,
                    EstimatedTokens = 130,
                    SizeKb = 5,
                    LastModified = DateTimeOffset.UtcNow,
                    ContentHash = "abc"
                }
            ]
        };

        var map = DocumentIndexService.ToDocumentMap(index);

        Assert.Single(map.Documents);
        Assert.Equal("docs/test.md", map.Documents[0].Path);
        Assert.Equal("Test", map.Documents[0].Title);
        Assert.Equal(5, map.Documents[0].SizeKb);
        Assert.Equal(2, map.Documents[0].Headings.Count);
        Assert.Equal("Overview", map.Documents[0].Headings[0]);
        Assert.Equal("First section", map.Documents[0].Preview);
    }

    [Fact]
    public void ResolveIndexPath_StillReturnsLegacyPath()
    {
        // ResolveIndexPath is still used by a few callers (e.g., GetDocumentMapTool
        // for fallback paths). It returns the legacy path even though the legacy
        // file is no longer written by EnsureNewLayoutAsync.
        var config = new SourceConfig { LocalDir = "docs/" };
        var path = DocumentIndexService.ResolveIndexPath("/base", config);
        Assert.Contains("_index.md", path);
    }

    [Fact]
    public async Task GetUpdateStatsAsync_ReportsCorrectCounts()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), "# A\n\nContent.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "b.md"), "# B\n\nContent.");

        var (source, coverage) = Configs();

        // No index yet - all files are new
        var (total1, changed1) = await _service.GetUpdateStatsAsync(_tempDir, source);
        Assert.Equal(2, total1);
        Assert.Equal(2, changed1);

        // Build the v2 layout
        await _service.EnsureNewLayoutAsync(_tempDir, source, coverage, forceRebuild: true);

        // Now all files are up to date
        var (total2, changed2) = await _service.GetUpdateStatsAsync(_tempDir, source);
        Assert.Equal(2, total2);
        Assert.Equal(0, changed2);
    }
}
