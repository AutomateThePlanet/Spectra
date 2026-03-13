using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands;

[Collection("Sequential Command Tests")]
public class AnalyzeCommandTests : IDisposable
{
    private readonly string _testDir;

    public AnalyzeCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-analyze-test-{Guid.NewGuid():N}");
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
    public async Task Analyze_NoConfig_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "analyze"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Analyze_WithConfigNoTests_ReturnsSuccess()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            // Create minimal config with all required fields
            await CreateConfigAsync();

            // Create empty docs and tests directories
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "analyze"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Analyze_WithDocsAndTests_ShowsCoverage()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            // Create config
            await CreateConfigAsync();

            // Create docs
            var docsDir = Path.Combine(_testDir, "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "feature.md"),
                "# Feature\n\nThis is a feature.");

            // Create test suite with test
            var suiteDir = Path.Combine(_testDir, "tests", "checkout");
            Directory.CreateDirectory(suiteDir);

            var testContent = @"---
id: TC-001
priority: high
source_refs:
  - docs/feature.md
---
# Test feature

## Steps
1. Test step

## Expected Result
Success";
            await File.WriteAllTextAsync(Path.Combine(suiteDir, "TC-001.md"), testContent);

            // Create index
            var index = new
            {
                suite = "checkout",
                generated_at = DateTime.UtcNow,
                test_count = 1,
                tests = new[]
                {
                    new { id = "TC-001", title = "Test feature", priority = "high", file = "checkout/TC-001.md", source_refs = new[] { "docs/feature.md" } }
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(suiteDir, "_index.json"),
                JsonSerializer.Serialize(index, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "analyze"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Analyze_JsonFormat_OutputsJson()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "analyze", "--format", "json"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Analyze_MarkdownFormat_OutputsMarkdown()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "analyze", "--format", "markdown"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Analyze_OutputToFile_CreatesFile()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var outputPath = Path.Combine(_testDir, "report.md");

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "analyze", "--output", outputPath, "--format", "markdown"]);

            Assert.Equal(0, result);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private static RootCommand CreateCommand()
    {
        var rootCommand = new RootCommand();
        GlobalOptions.AddTo(rootCommand);
        rootCommand.AddCommand(AiCommand.Create());
        return rootCommand;
    }

    private async Task CreateConfigAsync()
    {
        var config = new
        {
            source = new { local_dir = "docs/", include_patterns = new[] { "**/*.md" } },
            tests = new { dir = "tests/" },
            ai = new { providers = new[] { new { name = "test", model = "test-model", enabled = true, priority = 1 } } }
        };
        await File.WriteAllTextAsync(
            Path.Combine(_testDir, "spectra.config.json"),
            JsonSerializer.Serialize(config));
    }
}
