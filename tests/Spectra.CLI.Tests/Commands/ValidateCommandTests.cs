using System.CommandLine;
using Spectra.CLI.Commands.Validate;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands;

[Collection("Sequential Command Tests")]
public class ValidateCommandTests : IDisposable
{
    private readonly string _testDir;

    public ValidateCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-validate-test-{Guid.NewGuid():N}");
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
    public async Task Validate_NoTestsDirectory_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = CreateValidateCommand();
            var result = await command.InvokeAsync(["validate"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Validate_EmptyTestsDirectory_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var command = CreateValidateCommand();
            var result = await command.InvokeAsync(["validate"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Validate_ValidTestFile_ReturnsSuccess()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            // Create valid test file
            var suitePath = Path.Combine(_testDir, "tests", "checkout");
            Directory.CreateDirectory(suitePath);

            var testContent =
@"---
id: TC-001
priority: high
---
# Test checkout flow

## Steps
1. Add item to cart
2. Click checkout

## Expected Result
Order is placed successfully";

            await File.WriteAllTextAsync(Path.Combine(suitePath, "TC-001.md"), testContent);

            var command = CreateValidateCommand();
            var result = await command.InvokeAsync(["validate"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Validate_InvalidTestFile_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            // Create invalid test file (missing required fields)
            var suitePath = Path.Combine(_testDir, "tests", "checkout");
            Directory.CreateDirectory(suitePath);

            var testContent =
@"---
id: INVALID-ID
priority: high
---
# Test with invalid ID

## Steps
1. Do something

## Expected Result
Something happens";

            await File.WriteAllTextAsync(Path.Combine(suitePath, "TC-001.md"), testContent);

            var command = CreateValidateCommand();
            var result = await command.InvokeAsync(["validate"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Validate_WithSuiteOption_ValidatesOnlySuite()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            // Create two suites
            var suite1Path = Path.Combine(_testDir, "tests", "suite1");
            var suite2Path = Path.Combine(_testDir, "tests", "suite2");
            Directory.CreateDirectory(suite1Path);
            Directory.CreateDirectory(suite2Path);

            var validContent =
@"---
id: TC-001
priority: high
---
# Valid test

## Steps
1. Step

## Expected Result
Result";

            var invalidContent =
@"---
id: BAD-ID
priority: high
---
# Invalid test

## Steps
1. Step

## Expected Result
Result";

            await File.WriteAllTextAsync(Path.Combine(suite1Path, "TC-001.md"), validContent);
            await File.WriteAllTextAsync(Path.Combine(suite2Path, "TC-002.md"), invalidContent);

            // Validate only suite1 (valid)
            var command = CreateValidateCommand();
            var result = await command.InvokeAsync(["validate", "--suite", "suite1"]);

            Assert.Equal(0, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Validate_NonexistentSuite_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

            var command = CreateValidateCommand();
            var result = await command.InvokeAsync(["validate", "--suite", "nonexistent"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private static RootCommand CreateValidateCommand()
    {
        var rootCommand = new RootCommand();
        GlobalOptions.AddTo(rootCommand);
        rootCommand.AddCommand(new ValidateCommand());
        return rootCommand;
    }
}
