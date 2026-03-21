using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands;

[Collection("WorkingDirectory")]
public class UpdateCommandTests : IDisposable
{
    private readonly string _testDir;

    public UpdateCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-update-test-{Guid.NewGuid():N}");
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
    public async Task Update_NoConfig_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "update"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Update_WithNoTests_ReturnsSuccess()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            var suiteDir = Path.Combine(_testDir, "tests", "empty-suite");
            Directory.CreateDirectory(suiteDir);

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "update", "empty-suite", "--no-review"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Update_WithSuiteArgument_TargetsSpecificSuite()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));

            // Create suite1 and suite2
            var suite1Dir = Path.Combine(_testDir, "tests", "suite1");
            var suite2Dir = Path.Combine(_testDir, "tests", "suite2");
            Directory.CreateDirectory(suite1Dir);
            Directory.CreateDirectory(suite2Dir);

            // Create test in suite1
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
            await File.WriteAllTextAsync(Path.Combine(suite1Dir, "TC-001.md"), testContent);

            // Create index for suite1
            var index = new
            {
                suite = "suite1",
                generated_at = DateTime.UtcNow,
                test_count = 1,
                tests = new[]
                {
                    new { id = "TC-001", title = "Test feature", priority = "high", file = "suite1/TC-001.md", source_refs = new[] { "docs/feature.md" } }
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(suite1Dir, "_index.json"),
                JsonSerializer.Serialize(index, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));

            var command = CreateCommand();
            // suite is a positional argument, not an option
            var result = await command.InvokeAsync(["ai", "update", "suite1", "--no-review"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Update_WithDiffOption_ShowsDiff()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            var suiteDir = Path.Combine(_testDir, "tests", "diff-suite");
            Directory.CreateDirectory(suiteDir);

            var command = CreateCommand();
            var result = await command.InvokeAsync(["ai", "update", "diff-suite", "--diff", "--no-review"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Update_NonExistentSuite_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            await CreateConfigAsync();
            Directory.CreateDirectory(Path.Combine(_testDir, "docs"));
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var command = CreateCommand();
            // suite is a positional argument
            var result = await command.InvokeAsync(["ai", "update", "nonexistent", "--no-review"]);

            Assert.Equal(1, result);
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
