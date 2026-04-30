using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Source;

/// <summary>
/// Tests the spillover-write path inside <see cref="DocumentIndexService.EnsureNewLayoutAsync"/>
/// (Spec 040 §3.7 / Phase 5). When a single suite's <c>tokens_estimated</c> exceeds
/// <c>coverage.max_suite_tokens</c>, per-doc spillover files are written and the
/// suite's manifest entry gains a <c>spillover_files</c> list.
/// </summary>
public class SpilloverWriterTests : IDisposable
{
    private readonly string _tempDir;

    public SpilloverWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-spillover-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task EnsureNewLayoutAsync_SmallSuite_NoSpillover()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "a.md"), "# A\n\nSmall.");

        var service = new DocumentIndexService();
        var result = await service.EnsureNewLayoutAsync(
            _tempDir,
            new SourceConfig { LocalDir = "docs/" },
            new CoverageConfig { MaxSuiteTokens = 80_000 },
            forceRebuild: true);

        Assert.All(result.Manifest.Groups, g => Assert.Null(g.SpilloverFiles));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "docs", "_index", "docs")));
    }

    [Fact]
    public async Task EnsureNewLayoutAsync_OverThreshold_WritesSpillover()
    {
        var docsDir = Path.Combine(_tempDir, "docs", "many");
        Directory.CreateDirectory(docsDir);
        // 200 space-separated words per doc → ~260 estimated tokens (200*1.3).
        // 5 docs × 260 ≈ 1,300 tokens, comfortably over the 100-token threshold.
        var content = "# Doc\n\n" + string.Join(" ", Enumerable.Range(0, 200).Select(n => $"word{n}"));
        for (var i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(docsDir, $"d{i}.md"), content);
        }

        var service = new DocumentIndexService();
        var result = await service.EnsureNewLayoutAsync(
            _tempDir,
            new SourceConfig { LocalDir = "docs/" },
            new CoverageConfig { MaxSuiteTokens = 100 }, // tiny threshold
            forceRebuild: true);

        var ids = string.Join(",", result.Manifest.Groups.Select(g => g.Id));
        var manySuite = result.Manifest.Groups.FirstOrDefault(g => g.Id == "many");
        Assert.True(manySuite is not null,
            $"Expected suite 'many' in manifest. Documents={result.Manifest.TotalDocuments}, ids=[{ids}]");
        Assert.NotNull(manySuite!.SpilloverFiles);
        Assert.Equal(5, manySuite.SpilloverFiles!.Count);

        var spilloverDir = Path.Combine(_tempDir, "docs", "_index", "docs");
        Assert.True(Directory.Exists(spilloverDir));
        Assert.Equal(5, Directory.GetFiles(spilloverDir, "*.index.md").Length);
    }

    [Fact]
    public void SanitizeSpilloverFileName_ReplacesSlashesWithDoubleUnderscore()
    {
        var sanitized = DocumentIndexService.SanitizeSpilloverFileName(
            "docs/many/sub/doc.md");

        Assert.Equal("docs__many__sub__doc", sanitized);
    }

    [Fact]
    public void SanitizeSpilloverFileName_StripsMdExtension()
    {
        var sanitized = DocumentIndexService.SanitizeSpilloverFileName("docs/x.md");

        Assert.Equal("docs__x", sanitized);
    }

    [Fact]
    public void SanitizeSpilloverFileName_PreservesUnderscoresInOriginalPath()
    {
        var sanitized = DocumentIndexService.SanitizeSpilloverFileName(
            "docs/SM_GSG_Topics/manage.md");

        // Single underscores in the original path stay single; only the path
        // separators become double underscores.
        Assert.Equal("docs__SM_GSG_Topics__manage", sanitized);
    }
}
