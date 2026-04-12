using System.Text.Json;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools.Data;

public class AnalyzeCoverageGapsToolTests : IDisposable
{
    private readonly string _testDir;

    public AnalyzeCoverageGapsToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_NoTestsDir_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "docs"));

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("TESTS_DIR_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_NoDocsDir_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "test-cases"));

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("DOCS_DIR_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_SuiteNotFound_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "test-cases"));
        Directory.CreateDirectory(Path.Combine(_testDir, "docs"));

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"nonexistent\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("SUITE_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_DocumentNotCovered_ReturnsGap()
    {
        var testsDir = Path.Combine(_testDir, "test-cases", "auth");
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(docsDir);

        // Create empty index (no tests)
        await File.WriteAllTextAsync(Path.Combine(testsDir, "_index.json"), "{\"suite\":\"auth\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[]}");

        // Create uncovered document
        await File.WriteAllTextAsync(Path.Combine(docsDir, "feature.md"), "# Feature Documentation\n\nSome content");

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(1, response.GetProperty("data").GetProperty("docs_scanned").GetInt32());
        Assert.Equal(0, response.GetProperty("data").GetProperty("docs_covered").GetInt32());
        Assert.Equal(0, response.GetProperty("data").GetProperty("coverage_percent").GetInt32());

        var gaps = response.GetProperty("data").GetProperty("gaps");
        Assert.Equal(1, gaps.GetArrayLength());
        Assert.Equal("Feature Documentation", gaps[0].GetProperty("document_title").GetString());
    }

    [Fact]
    public async Task Execute_DocumentCovered_NotInGaps()
    {
        var testsDir = Path.Combine(_testDir, "test-cases", "auth");
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(docsDir);

        // Create index with source_ref
        var indexContent = """
            {
                "suite": "auth",
                "generated_at": "2026-01-01T00:00:00Z",
                "tests": [
                    {
                        "id": "TC-001",
                        "file": "tc-001.md",
                        "title": "Test",
                        "priority": "high",
                        "tags": [],
                        "source_refs": ["docs/feature.md"]
                    }
                ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(testsDir, "_index.json"), indexContent);

        // Create covered document
        await File.WriteAllTextAsync(Path.Combine(docsDir, "feature.md"), "# Feature\n\nContent");

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(1, response.GetProperty("data").GetProperty("docs_scanned").GetInt32());
        Assert.Equal(1, response.GetProperty("data").GetProperty("docs_covered").GetInt32());
        Assert.Equal(100, response.GetProperty("data").GetProperty("coverage_percent").GetInt32());
        Assert.Equal(0, response.GetProperty("data").GetProperty("gaps").GetArrayLength());
    }

    [Fact]
    public async Task Execute_LargeDocument_HighSeverity()
    {
        var testsDir = Path.Combine(_testDir, "test-cases", "auth");
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(docsDir);

        await File.WriteAllTextAsync(Path.Combine(testsDir, "_index.json"), "{\"suite\":\"auth\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[]}");

        // Create large document (>10KB)
        var content = "# Large Document\n\n" + new string('x', 12000);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "large.md"), content);

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var gaps = response.GetProperty("data").GetProperty("gaps");
        Assert.Equal("high", gaps[0].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task Execute_ManyHeadings_HighSeverity()
    {
        var testsDir = Path.Combine(_testDir, "test-cases", "auth");
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(docsDir);

        await File.WriteAllTextAsync(Path.Combine(testsDir, "_index.json"), "{\"suite\":\"auth\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[]}");

        // Create document with many headings (>5)
        var content = """
            # Main Title
            ## Section 1
            ## Section 2
            ## Section 3
            ## Section 4
            ## Section 5
            ## Section 6
            """;
        await File.WriteAllTextAsync(Path.Combine(docsDir, "headings.md"), content);

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var gaps = response.GetProperty("data").GetProperty("gaps");
        Assert.Equal("high", gaps[0].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task Execute_CustomDocsPath_Works()
    {
        var testsDir = Path.Combine(_testDir, "test-cases", "auth");
        var customDocsDir = Path.Combine(_testDir, "documentation");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(customDocsDir);

        await File.WriteAllTextAsync(Path.Combine(testsDir, "_index.json"), "{\"suite\":\"auth\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[]}");
        await File.WriteAllTextAsync(Path.Combine(customDocsDir, "feature.md"), "# Feature\n\nContent");

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"docs_path\":\"documentation\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(1, response.GetProperty("data").GetProperty("docs_scanned").GetInt32());
    }

    [Fact]
    public async Task Execute_ExtractsTitleFromH1()
    {
        var testsDir = Path.Combine(_testDir, "test-cases", "auth");
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(docsDir);

        await File.WriteAllTextAsync(Path.Combine(testsDir, "_index.json"), "{\"suite\":\"auth\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[]}");
        await File.WriteAllTextAsync(Path.Combine(docsDir, "feature.md"), "# My Feature Title\n\nContent");

        var tool = new AnalyzeCoverageGapsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var gaps = response.GetProperty("data").GetProperty("gaps");
        Assert.Equal("My Feature Title", gaps[0].GetProperty("document_title").GetString());
    }
}
