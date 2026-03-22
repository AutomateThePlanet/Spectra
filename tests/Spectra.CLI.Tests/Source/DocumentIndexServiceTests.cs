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
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureIndexAsync_CreatesIndexFromDocs()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), "# Test Doc\n\n## Section One\n\nSome content here.");

        var config = new SourceConfig();
        var index = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: true);

        Assert.Equal(1, index.TotalDocuments);
        Assert.Equal("Test Doc", index.Entries[0].Title);

        // Verify file was written
        var indexPath = DocumentIndexService.ResolveIndexPath(_tempDir, config);
        Assert.True(File.Exists(indexPath));
    }

    [Fact]
    public async Task EnsureIndexAsync_IncrementalSkipsUnchangedFiles()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), "# Doc A\n\nContent A.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "b.md"), "# Doc B\n\nContent B.");

        var config = new SourceConfig();

        // Initial build
        var index1 = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: true);
        Assert.Equal(2, index1.TotalDocuments);

        // Incremental - no changes
        var index2 = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: false);
        Assert.Equal(2, index2.TotalDocuments);

        // Modify one file
        var updatedContent = "# Doc A Updated\n\nNew content here.";
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), updatedContent);

        var index3 = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: false);
        Assert.Equal(2, index3.TotalDocuments);

        var updatedEntry = index3.Entries.First(e => e.Path == "docs/a.md");
        Assert.Equal("Doc A Updated", updatedEntry.Title);
    }

    [Fact]
    public async Task EnsureIndexAsync_HandlesNoDocs()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);

        var config = new SourceConfig();
        var index = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: true);

        Assert.Equal(0, index.TotalDocuments);
    }

    [Fact]
    public async Task EnsureIndexAsync_ForceRebuild()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), "# Test\n\nContent.");

        var config = new SourceConfig();

        // Build once
        await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: false);

        // Force rebuild
        var index = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: true);
        Assert.Equal(1, index.TotalDocuments);
    }

    [Fact]
    public async Task EnsureIndexAsync_HandlesRemovedFiles()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        var filePath = Path.Combine(docsDir, "temp.md");
        await File.WriteAllTextAsync(filePath, "# Temp\n\nContent.");

        var config = new SourceConfig();
        var index1 = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: true);
        Assert.Equal(1, index1.TotalDocuments);

        // Remove file
        File.Delete(filePath);
        var index2 = await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: false);
        Assert.Equal(0, index2.TotalDocuments);
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
    public void ResolveIndexPath_UsesDocIndexFromConfig()
    {
        var config = new SourceConfig { DocIndex = "custom/_index.md" };
        var path = DocumentIndexService.ResolveIndexPath("/base", config);
        Assert.Contains("custom", path);
        Assert.Contains("_index.md", path);
    }

    [Fact]
    public void ResolveIndexPath_DefaultsToLocalDir()
    {
        var config = new SourceConfig();
        var path = DocumentIndexService.ResolveIndexPath("/base", config);
        Assert.Contains("docs", path);
        Assert.Contains("_index.md", path);
    }

    [Fact]
    public async Task GetUpdateStatsAsync_ReportsCorrectCounts()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), "# A\n\nContent.");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "b.md"), "# B\n\nContent.");

        var config = new SourceConfig();

        // No index yet - all files are new
        var (total1, changed1) = await _service.GetUpdateStatsAsync(_tempDir, config);
        Assert.Equal(2, total1);
        Assert.Equal(2, changed1);

        // Build index
        await _service.EnsureIndexAsync(_tempDir, config, forceRebuild: true);

        // Now all files are up to date
        var (total2, changed2) = await _service.GetUpdateStatsAsync(_tempDir, config);
        Assert.Equal(2, total2);
        Assert.Equal(0, changed2);
    }
}
