using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Index;
using Spectra.CLI.Options;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands;

[Collection("WorkingDirectory")]
public class IndexCommandTests : IDisposable
{
    private readonly string _testDir;

    public IndexCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-index-test-{Guid.NewGuid():N}");
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
    public async Task Index_NoTestsDirectory_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = CreateIndexCommand();
            var result = await command.InvokeAsync(["index"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Index_CreatesIndexFile()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var suitePath = await CreateTestSuiteAsync("checkout", "TC-001");

            var command = CreateIndexCommand();
            var result = await command.InvokeAsync(["index"]);

            Assert.Equal(0, result);

            var indexPath = Path.Combine(suitePath, "_index.json");
            Assert.True(File.Exists(indexPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Index_IndexContainsTestMetadata()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var suitePath = await CreateTestSuiteAsync("checkout", "TC-001", "TC-002");

            var command = CreateIndexCommand();
            await command.InvokeAsync(["index"]);

            var indexPath = Path.Combine(suitePath, "_index.json");
            var json = await File.ReadAllTextAsync(indexPath);
            var index = JsonSerializer.Deserialize<MetadataIndex>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(index);
            Assert.Equal("checkout", index.Suite);
            Assert.Equal(2, index.TestCount);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Index_WithSuiteOption_IndexesOnlySuite()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var suite1Path = await CreateTestSuiteAsync("suite1", "TC-001");
            var suite2Path = await CreateTestSuiteAsync("suite2", "TC-002");

            var command = CreateIndexCommand();
            await command.InvokeAsync(["index", "--suite", "suite1"]);

            Assert.True(File.Exists(Path.Combine(suite1Path, "_index.json")));
            Assert.False(File.Exists(Path.Combine(suite2Path, "_index.json")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Index_RebuildOption_ForcesRebuild()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var suitePath = await CreateTestSuiteAsync("checkout", "TC-001");

            // First index
            var command = CreateIndexCommand();
            await command.InvokeAsync(["index"]);

            var indexPath = Path.Combine(suitePath, "_index.json");
            var firstModified = File.GetLastWriteTimeUtc(indexPath);

            // Wait a bit to ensure different timestamps
            await Task.Delay(100);

            // Rebuild
            await command.InvokeAsync(["index", "--rebuild"]);

            var secondModified = File.GetLastWriteTimeUtc(indexPath);
            Assert.True(secondModified > firstModified);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Index_DryRunOption_DoesNotWriteFile()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var suitePath = await CreateTestSuiteAsync("checkout", "TC-001");

            var command = CreateIndexCommand();
            await command.InvokeAsync(["index", "--dry-run"]);

            var indexPath = Path.Combine(suitePath, "_index.json");
            Assert.False(File.Exists(indexPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private async Task<string> CreateTestSuiteAsync(string suiteName, params string[] testIds)
    {
        var suitePath = Path.Combine(_testDir, "tests", suiteName);
        Directory.CreateDirectory(suitePath);

        foreach (var id in testIds)
        {
            var testContent =
$@"---
id: {id}
priority: high
---
# Test {id}

## Steps
1. Do something

## Expected Result
Something happens";

            await File.WriteAllTextAsync(Path.Combine(suitePath, $"{id}.md"), testContent);
        }

        return suitePath;
    }

    private static RootCommand CreateIndexCommand()
    {
        var rootCommand = new RootCommand();
        GlobalOptions.AddTo(rootCommand);
        rootCommand.AddCommand(new IndexCommand());
        return rootCommand;
    }
}
