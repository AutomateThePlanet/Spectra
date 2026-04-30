using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Source;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands;

[Collection("WorkingDirectory")]
public class DocsIndexCommandTests : IDisposable
{
    private readonly string _testDir;

    public DocsIndexCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-docsindex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                // Allow file handles to be fully released before cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — temp directory may still be locked on Windows
        }
    }

    [Fact]
    public async Task DocsIndex_NoConfig_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = new RootCommand();
            command.AddCommand(new DocsCommand());
            var result = await command.InvokeAsync(["docs", "index"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task DocsIndex_WithDocs_CreatesIndex()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            // Setup
            var docsDir = Path.Combine(_testDir, "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(Path.Combine(docsDir, "feature.md"),
                "# Feature\n\n## Overview\n\nThis feature does something.\n\n## Details\n\nMore details.");

            var config = SpectraConfig.Default;
            await File.WriteAllTextAsync(
                Path.Combine(_testDir, "spectra.config.json"),
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            Directory.SetCurrentDirectory(_testDir);

            var command = new RootCommand();
            command.AddCommand(new DocsCommand());
            var result = await command.InvokeAsync(["docs", "index"]);

            Assert.Equal(0, result);

            // Spec 040: indexer now writes the v2 layout under
            // docs/_index/ rather than a single docs/_index.md. Verify all
            // three artifacts.
            var indexDir = Path.Combine(_testDir, "docs", "_index");
            Assert.True(Directory.Exists(indexDir));
            Assert.True(File.Exists(Path.Combine(indexDir, "_manifest.yaml")));
            Assert.True(File.Exists(Path.Combine(indexDir, "_checksums.json")));
            Assert.True(Directory.Exists(Path.Combine(indexDir, "groups")));

            // The "feature.md" doc lives directly in docs/ → assigned to suite
            // _root → its content rendered into groups/_root.index.md.
            var rootSuiteFile = Path.Combine(indexDir, "groups", "_root.index.md");
            Assert.True(File.Exists(rootSuiteFile));
            var content = await File.ReadAllTextAsync(rootSuiteFile);
            Assert.Contains("Feature", content);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task DocsIndex_Force_RebuildsFully()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            var docsDir = Path.Combine(_testDir, "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(Path.Combine(docsDir, "test.md"), "# Test\n\nContent.");

            var config = SpectraConfig.Default;
            await File.WriteAllTextAsync(
                Path.Combine(_testDir, "spectra.config.json"),
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            Directory.SetCurrentDirectory(_testDir);

            var command = new RootCommand();
            command.AddCommand(new DocsCommand());

            // First run
            await command.InvokeAsync(["docs", "index"]);

            // Force rebuild
            var result = await command.InvokeAsync(["docs", "index", "--force"]);
            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }
}
