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

            // Verify index was created
            var indexPath = DocumentIndexService.ResolveIndexPath(_testDir, config.Source);
            Assert.True(File.Exists(indexPath));

            var content = await File.ReadAllTextAsync(indexPath);
            Assert.Contains("# Documentation Index", content);
            Assert.Contains("Feature", content);
            Assert.Contains("SPECTRA_INDEX_CHECKSUMS", content);
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
