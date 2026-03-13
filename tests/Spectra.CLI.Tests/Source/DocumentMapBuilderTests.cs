using Spectra.CLI.Source;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Source;

public class DocumentMapBuilderTests : IDisposable
{
    private readonly string _testDir;

    public DocumentMapBuilderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-docmap-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task BuildAsync_EmptyDirectory_ReturnsEmptyMap()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        var config = new SourceConfig { LocalDir = "docs" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Empty(map.Documents);
        Assert.Equal(0, map.TotalSizeKb);
    }

    [Fact]
    public async Task BuildAsync_WithMarkdownFiles_ReturnsEntries()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        var content = """
            # Test Document

            This is a test document.

            ## Section 1

            Some content here.
            """;

        await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), content);

        var config = new SourceConfig { LocalDir = "docs" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Single(map.Documents);
        var entry = map.Documents[0];
        Assert.Equal("docs/test.md", entry.Path);
        Assert.Equal("Test Document", entry.Title);
        Assert.Contains("Section 1", entry.Headings);
    }

    [Fact]
    public async Task BuildAsync_WithMultipleFiles_ReturnsSortedEntries()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        await File.WriteAllTextAsync(Path.Combine(docsDir, "zebra.md"), "# Zebra");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "apple.md"), "# Apple");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "middle.md"), "# Middle");

        var config = new SourceConfig { LocalDir = "docs" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Equal(3, map.Documents.Count);
        Assert.Equal("docs/apple.md", map.Documents[0].Path);
        Assert.Equal("docs/middle.md", map.Documents[1].Path);
        Assert.Equal("docs/zebra.md", map.Documents[2].Path);
    }

    [Fact]
    public async Task BuildAsync_WithNestedDirectories_FindsAllFiles()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        var featureDir = Path.Combine(docsDir, "features");
        var checkoutDir = Path.Combine(featureDir, "checkout");
        Directory.CreateDirectory(checkoutDir);

        await File.WriteAllTextAsync(Path.Combine(docsDir, "intro.md"), "# Intro");
        await File.WriteAllTextAsync(Path.Combine(featureDir, "overview.md"), "# Overview");
        await File.WriteAllTextAsync(Path.Combine(checkoutDir, "flow.md"), "# Checkout Flow");

        var config = new SourceConfig { LocalDir = "docs" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Equal(3, map.Documents.Count);
        Assert.Contains(map.Documents, e => e.Path == "docs/intro.md");
        Assert.Contains(map.Documents, e => e.Path == "docs/features/overview.md");
        Assert.Contains(map.Documents, e => e.Path == "docs/features/checkout/flow.md");
    }

    [Fact]
    public async Task BuildAsync_WithExcludePatterns_FiltersFiles()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        await File.WriteAllTextAsync(Path.Combine(docsDir, "guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "CHANGELOG.md"), "# Changelog");

        var config = new SourceConfig
        {
            LocalDir = "docs",
            IncludePatterns = ["**/*.md"],
            ExcludePatterns = ["**/CHANGELOG.md"]
        };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Single(map.Documents);
        Assert.Equal("docs/guide.md", map.Documents[0].Path);
    }

    [Fact]
    public async Task BuildAsync_CalculatesTotalSize()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        var content = new string('x', 2048); // 2KB
        await File.WriteAllTextAsync(Path.Combine(docsDir, "large.md"), content);

        var config = new SourceConfig { LocalDir = "docs" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Single(map.Documents);
        Assert.True(map.TotalSizeKb >= 1);
    }

    [Fact]
    public async Task BuildAsync_ExtractsPreview()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        var content = """
            # Test Document

            This is the first paragraph with some content that should appear in the preview.

            ## Section

            More content here.
            """;

        await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), content);

        var config = new SourceConfig { LocalDir = "docs" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Single(map.Documents);
        Assert.Contains("first paragraph", map.Documents[0].Preview);
    }

    [Fact]
    public async Task BuildAsync_NonexistentDirectory_ReturnsEmptyMap()
    {
        var config = new SourceConfig { LocalDir = "nonexistent" };
        var builder = new DocumentMapBuilder(config);

        var map = await builder.BuildAsync(_testDir);

        Assert.Empty(map.Documents);
    }

    [Fact]
    public async Task BuildFromFilesAsync_WithSpecificFiles_ReturnsOnlyThoseFiles()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        await File.WriteAllTextAsync(Path.Combine(docsDir, "file1.md"), "# File 1");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "file2.md"), "# File 2");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "file3.md"), "# File 3");

        var builder = new DocumentMapBuilder();

        var map = await builder.BuildFromFilesAsync(
            [Path.Combine(docsDir, "file1.md"), Path.Combine(docsDir, "file3.md")],
            _testDir);

        Assert.Equal(2, map.Documents.Count);
        Assert.Contains(map.Documents, e => e.Path.Contains("file1.md"));
        Assert.Contains(map.Documents, e => e.Path.Contains("file3.md"));
        Assert.DoesNotContain(map.Documents, e => e.Path.Contains("file2.md"));
    }

    [Fact]
    public void GetSummary_ReturnsFormattedString()
    {
        var map = new Spectra.Core.Models.DocumentMap
        {
            Documents =
            [
                new Spectra.Core.Models.DocumentEntry
                {
                    Path = "docs/test1.md",
                    Title = "Test 1",
                    SizeKb = 5,
                    Headings = [],
                    Preview = ""
                },
                new Spectra.Core.Models.DocumentEntry
                {
                    Path = "docs/test2.md",
                    Title = "Test 2",
                    SizeKb = 3,
                    Headings = [],
                    Preview = ""
                }
            ],
            TotalSizeKb = 8
        };

        var summary = DocumentMapBuilder.GetSummary(map);

        Assert.Contains("2 files", summary);
        Assert.Contains("8 KB", summary);
        Assert.Contains(".md", summary);
    }
}
