using System.CommandLine;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands;

[Collection("Sequential Command Tests")]
public class GenerateCommandTests : IDisposable
{
    private readonly string _testDir;

    public GenerateCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-generate-test-{Guid.NewGuid():N}");
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
    public async Task Generate_NoConfig_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = CreateAiCommand();
            var result = await command.InvokeAsync(["ai", "generate", "checkout"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Generate_NoSourceDocs_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateConfigAsync();

            // No docs directory
            var command = CreateAiCommand();
            var result = await command.InvokeAsync(["ai", "generate", "checkout"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Generate_WithDryRun_DoesNotCreateFiles()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await SetupTestEnvironmentAsync();

            var command = CreateAiCommand();
            var result = await command.InvokeAsync(["ai", "generate", "checkout", "--dry-run"]);

            // Should succeed with dry-run (mock agent)
            Assert.Equal(0, result);

            // No test files should be created
            var testsPath = Path.Combine(_testDir, "tests", "checkout");
            if (Directory.Exists(testsPath))
            {
                var files = Directory.GetFiles(testsPath, "TC-*.md");
                Assert.Empty(files);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Generate_WithNoReview_SkipsInteractivePrompt()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await SetupTestEnvironmentAsync();

            var command = CreateAiCommand();
            var result = await command.InvokeAsync(["ai", "generate", "checkout", "--no-review"]);

            // Should succeed
            Assert.Equal(0, result);

            // Test files should be created
            var testsPath = Path.Combine(_testDir, "tests", "checkout");
            Assert.True(Directory.Exists(testsPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Generate_WithCountOption_PassesToHandler()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await SetupTestEnvironmentAsync();

            var command = CreateAiCommand();
            var result = await command.InvokeAsync(["ai", "generate", "checkout", "-n", "10", "--dry-run"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Generate_CreatesIndexFile()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await SetupTestEnvironmentAsync();

            var command = CreateAiCommand();
            await command.InvokeAsync(["ai", "generate", "checkout", "--no-review"]);

            // Index should be created/updated
            var indexPath = Path.Combine(_testDir, "tests", "checkout", "_index.json");
            Assert.True(File.Exists(indexPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Generate_WithExistingTests_AvoidsIdConflict()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await SetupTestEnvironmentAsync();

            // Create an existing test
            var suitePath = Path.Combine(_testDir, "tests", "checkout");
            Directory.CreateDirectory(suitePath);

            var existingTest = """
                ---
                id: TC-100
                priority: high
                ---
                # Existing test

                ## Steps
                1. Do something

                ## Expected Result
                Something happens
                """;

            await File.WriteAllTextAsync(Path.Combine(suitePath, "TC-100.md"), existingTest);

            var command = CreateAiCommand();
            await command.InvokeAsync(["ai", "generate", "checkout", "--no-review"]);

            // New tests should have IDs > 100
            var files = Directory.GetFiles(suitePath, "TC-*.md")
                .Where(f => !Path.GetFileName(f).Equals("TC-100.md", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                Assert.DoesNotContain("id: TC-100", content);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private async Task CreateConfigAsync()
    {
        var config = """
            {
              "source": {
                "mode": "local",
                "local_dir": "docs/",
                "include_patterns": ["**/*.md"],
                "exclude_patterns": []
              },
              "tests": {
                "dir": "tests/",
                "id_prefix": "TC",
                "id_start": 100
              },
              "ai": {
                "providers": [
                  {
                    "name": "mock",
                    "model": "mock-model",
                    "enabled": true,
                    "priority": 1
                  }
                ],
                "fallback_strategy": "auto"
              },
              "generation": {
                "default_count": 5,
                "require_review": true
              },
              "validation": {
                "id_pattern": "^TC-\\d{3,}$",
                "required_fields": ["id", "priority"],
                "allowed_priorities": ["high", "medium", "low"],
                "max_steps": 20
              }
            }
            """;

        await File.WriteAllTextAsync(Path.Combine(_testDir, "spectra.config.json"), config);
    }

    private async Task SetupTestEnvironmentAsync()
    {
        // Create config
        await CreateConfigAsync();

        // Create docs directory with sample content
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);

        var sampleDoc = """
            # Checkout Feature

            ## Overview
            Users can complete their purchases through our checkout flow.

            ## Steps
            1. Add items to cart
            2. Click checkout
            3. Enter shipping details
            4. Enter payment info
            5. Confirm order

            ## Validations
            - Cart must not be empty
            - Valid shipping address required
            - Valid payment method required
            """;

        await File.WriteAllTextAsync(Path.Combine(docsDir, "checkout.md"), sampleDoc);

        // Create tests directory
        Directory.CreateDirectory(Path.Combine(_testDir, "tests"));
    }

    private static RootCommand CreateAiCommand()
    {
        var rootCommand = new RootCommand();
        GlobalOptions.AddTo(rootCommand);
        rootCommand.AddCommand(AiCommand.Create());
        return rootCommand;
    }
}
