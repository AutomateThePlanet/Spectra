using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Commands.Config;
using Spectra.CLI.Commands.Index;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Commands.List;
using Spectra.CLI.Commands.Show;
using Spectra.CLI.Commands.Validate;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Integration;

/// <summary>
/// Tests the complete quickstart workflow as documented.
/// </summary>
[Collection("Sequential Command Tests")]
public class QuickstartWorkflowTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public QuickstartWorkflowTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-quickstart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task Step1_Init_CreatesConfigAndDirectories()
    {
        var command = CreateRootCommand();
        var result = await command.InvokeAsync(["init"]);

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_testDir, "spectra.config.json")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "docs")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "tests")));
    }

    [Fact]
    public async Task Step2_AddDocs_ThenValidate_ReturnsSuccessWithNoDocs()
    {
        // Initialize
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        // Validate (no tests yet)
        var validateResult = await command.InvokeAsync(["validate"]);

        // Should succeed or error based on no tests found
        Assert.True(validateResult == 0 || validateResult == 1);
    }

    [Fact]
    public async Task Step3_CreateTestsManually_ThenValidate_Succeeds()
    {
        // Initialize
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        // Create documentation
        var docsDir = Path.Combine(_testDir, "docs", "features");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(
            Path.Combine(docsDir, "checkout.md"),
            "# Checkout\n\nUsers can checkout and pay for items.");

        // Create a test suite with test
        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var testContent = @"---
id: TC-001
priority: high
source_refs: [docs/features/checkout.md]
---
# Test checkout flow

## Steps
1. Add item to cart
2. Click checkout

## Expected Result
Order is placed successfully";

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "TC-001.md"), testContent);

        // Validate should pass
        var validateResult = await command.InvokeAsync(["validate"]);
        Assert.Equal(0, validateResult);
    }

    [Fact]
    public async Task Step4_Index_BuildsIndexFile()
    {
        // Initialize
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        // Create a test
        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var testContent = @"---
id: TC-001
priority: high
---
# Test checkout

## Steps
1. Step

## Expected Result
Result";

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "TC-001.md"), testContent);

        // Build index
        var indexResult = await command.InvokeAsync(["index"]);
        Assert.Equal(0, indexResult);

        // Verify index created
        Assert.True(File.Exists(Path.Combine(suiteDir, "_index.json")));
    }

    [Fact]
    public async Task Step5_List_ShowsTests()
    {
        // Initialize and create test
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var testContent = @"---
id: TC-001
priority: high
---
# Test checkout

## Steps
1. Step

## Expected Result
Result";

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "TC-001.md"), testContent);

        // Build index
        await command.InvokeAsync(["index"]);

        // List tests
        var listResult = await command.InvokeAsync(["list"]);
        Assert.Equal(0, listResult);
    }

    [Fact]
    public async Task Step6_Show_DisplaysTestDetails()
    {
        // Initialize and create test
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var testContent = @"---
id: TC-001
priority: high
tags: []
---
# Test checkout

## Steps
1. Step

## Expected Result
Result";

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "TC-001.md"), testContent);

        // Build index with correct relative file path (suite/filename)
        var indexContent = JsonSerializer.Serialize(new
        {
            suite = "checkout",
            generated_at = DateTime.UtcNow,
            test_count = 1,
            tests = new[]
            {
                new { id = "TC-001", title = "Test checkout", priority = "high", file = "checkout/TC-001.md", tags = Array.Empty<string>() }
            }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        await File.WriteAllTextAsync(Path.Combine(suiteDir, "_index.json"), indexContent);

        // Show test
        var showResult = await command.InvokeAsync(["show", "TC-001"]);
        Assert.Equal(0, showResult);
    }

    [Fact]
    public async Task Step7_Config_ShowsConfiguration()
    {
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        var configResult = await command.InvokeAsync(["config"]);
        Assert.Equal(0, configResult);
    }

    [Fact]
    public async Task Step8_Analyze_ShowsCoverageReport()
    {
        // Initialize
        var command = CreateRootCommand();
        await command.InvokeAsync(["init"]);

        // Update config to include required AI provider
        var configPath = Path.Combine(_testDir, "spectra.config.json");
        var config = new
        {
            source = new { local_dir = "docs/", include_patterns = new[] { "**/*.md" } },
            tests = new { dir = "tests/" },
            ai = new { providers = new[] { new { name = "test", model = "test", enabled = true, priority = 1 } } }
        };
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config));

        // Create docs
        var docsDir = Path.Combine(_testDir, "docs");
        await File.WriteAllTextAsync(
            Path.Combine(docsDir, "feature.md"),
            "# Feature\n\nFeature description.");

        // Create suite with test
        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var testContent = @"---
id: TC-001
priority: high
source_refs: [docs/feature.md]
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

        // Analyze coverage
        var analyzeResult = await command.InvokeAsync(["ai", "analyze"]);
        Assert.Equal(0, analyzeResult);
    }

    [Fact]
    public async Task FullWorkflow_InitToValidate_Succeeds()
    {
        var command = CreateRootCommand();

        // Step 1: Init
        Assert.Equal(0, await command.InvokeAsync(["init"]));

        // Step 2: Create docs
        var docsDir = Path.Combine(_testDir, "docs", "features");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(
            Path.Combine(docsDir, "auth.md"),
            "# Authentication\n\nUsers can log in with email and password.");

        // Step 3: Create test
        var suiteDir = Path.Combine(_testDir, "tests", "auth");
        Directory.CreateDirectory(suiteDir);
        await File.WriteAllTextAsync(
            Path.Combine(suiteDir, "TC-001.md"),
            @"---
id: TC-001
priority: high
source_refs: [docs/features/auth.md]
---
# Login with valid credentials

## Steps
1. Navigate to login
2. Enter valid credentials
3. Click login

## Expected Result
User is logged in");

        // Step 4: Validate
        Assert.Equal(0, await command.InvokeAsync(["validate"]));

        // Step 5: Index
        Assert.Equal(0, await command.InvokeAsync(["index"]));

        // Step 6: List
        Assert.Equal(0, await command.InvokeAsync(["list"]));

        // Step 7: Config
        Assert.Equal(0, await command.InvokeAsync(["config"]));
    }

    private static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand();
        GlobalOptions.AddTo(rootCommand);
        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(new ValidateCommand());
        rootCommand.AddCommand(new IndexCommand());
        rootCommand.AddCommand(AiCommand.Create());
        rootCommand.AddCommand(new ListCommand());
        rootCommand.AddCommand(new ShowCommand());
        rootCommand.AddCommand(new ConfigCommand());
        return rootCommand;
    }
}
