using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Dashboard;
using Spectra.CLI.Options;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Dashboard;

/// <summary>
/// Integration tests for DashboardCommand.
/// </summary>
[Collection("WorkingDirectory")]
public class DashboardCommandTests : IDisposable
{
    private readonly string _testDir;

    public DashboardCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-dashboard-cmd-test-{Guid.NewGuid():N}");
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
    public async Task Dashboard_NoSuites_ReturnsError()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            var command = CreateRootCommand();
            var result = await command.InvokeAsync(["dashboard"]);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Dashboard_WithSuites_GeneratesSite()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateSuiteIndexAsync("checkout", 2);

            var outputPath = Path.Combine(_testDir, "site");
            var command = CreateRootCommand();
            var result = await command.InvokeAsync(["dashboard", "--output", outputPath]);

            Assert.Equal(0, result);
            Assert.True(File.Exists(Path.Combine(outputPath, "index.html")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Dashboard_CreatesStylesAndScripts()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateSuiteIndexAsync("checkout", 1);

            var outputPath = Path.Combine(_testDir, "site");
            var command = CreateRootCommand();
            await command.InvokeAsync(["dashboard", "--output", outputPath]);

            Assert.True(File.Exists(Path.Combine(outputPath, "styles", "main.css")));
            Assert.True(File.Exists(Path.Combine(outputPath, "scripts", "app.js")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Dashboard_WithTitleOption_OverridesRepositoryName()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateSuiteIndexAsync("checkout", 1);

            var outputPath = Path.Combine(_testDir, "site");
            var command = CreateRootCommand();
            await command.InvokeAsync(["dashboard", "--output", outputPath, "--title", "My Test Project"]);

            var html = await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html"));
            Assert.Contains("My Test Project", html);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Dashboard_DryRunOption_DoesNotWrite()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateSuiteIndexAsync("checkout", 1);

            var outputPath = Path.Combine(_testDir, "site");
            var command = CreateRootCommand();
            var result = await command.InvokeAsync(["dashboard", "--output", outputPath, "--dry-run"]);

            Assert.Equal(0, result);
            Assert.False(Directory.Exists(outputPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Dashboard_DefaultOutput_UsesSiteDirectory()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateSuiteIndexAsync("checkout", 1);

            var command = CreateRootCommand();
            var result = await command.InvokeAsync(["dashboard"]);

            Assert.Equal(0, result);
            Assert.True(File.Exists(Path.Combine(_testDir, "site", "index.html")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Dashboard_WithMultipleSuites_IncludesAllInOutput()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            await CreateSuiteIndexAsync("checkout", 2);
            await CreateSuiteIndexAsync("payments", 3);

            var outputPath = Path.Combine(_testDir, "site");
            var command = CreateRootCommand();
            await command.InvokeAsync(["dashboard", "--output", outputPath]);

            var html = await File.ReadAllTextAsync(Path.Combine(outputPath, "index.html"));
            Assert.Contains("checkout", html);
            Assert.Contains("payments", html);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private async Task CreateSuiteIndexAsync(string suiteName, int testCount)
    {
        var suitePath = Path.Combine(_testDir, "test-cases", suiteName);
        Directory.CreateDirectory(suitePath);

        var index = new MetadataIndex
        {
            Suite = suiteName,
            GeneratedAt = DateTime.UtcNow,
            Tests = Enumerable.Range(1, testCount).Select(i => new TestIndexEntry
            {
                Id = $"TC-{i:000}",
                Title = $"Test {i}",
                File = $"TC-{i:000}.md",
                Priority = "high",
                Tags = []
            }).ToList()
        };

        await File.WriteAllTextAsync(
            Path.Combine(suitePath, "_index.json"),
            JsonSerializer.Serialize(index, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }));
    }

    private static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand();
        GlobalOptions.AddTo(rootCommand);
        rootCommand.AddCommand(new DashboardCommand());
        return rootCommand;
    }
}
