using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Options;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands.Docs;

[Collection("WorkingDirectory")]
public class DocsListSuitesHandlerTests : IDisposable
{
    private readonly string _testDir;

    public DocsListSuitesHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-list-suites-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch (IOException) { }
    }

    private async Task SeedAsync(params string[] docPaths)
    {
        var config = SpectraConfig.Default;
        await File.WriteAllTextAsync(
            Path.Combine(_testDir, "spectra.config.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        foreach (var path in docPaths)
        {
            var full = Path.Combine(_testDir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, $"# {Path.GetFileNameWithoutExtension(full)}\n\nContent.");
        }
    }

    private async Task<int> RunAsync(params string[] args)
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var command = new RootCommand();
            GlobalOptions.AddTo(command);
            command.AddCommand(new DocsCommand());
            return await command.InvokeAsync(args);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task ListSuites_NoConfig_ReturnsError()
    {
        var exitCode = await RunAsync("docs", "list-suites");
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ListSuites_NoManifest_ReturnsError()
    {
        await SeedAsync();
        var exitCode = await RunAsync("docs", "list-suites");
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ListSuites_WithManifest_ReturnsSuccess()
    {
        await SeedAsync("docs/checkout/a.md", "docs/payments/b.md");
        await RunAsync("docs", "index", "--skip-criteria");

        var exitCode = await RunAsync("docs", "list-suites");
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ListSuites_JsonOutput_IsParseable()
    {
        await SeedAsync("docs/checkout/a.md");
        await RunAsync("docs", "index", "--skip-criteria");

        // Capture stdout via redirect.
        var oldOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            await RunAsync("docs", "list-suites", "--output-format", "json");
        }
        finally
        {
            Console.SetOut(oldOut);
        }

        var output = sw.ToString();
        // Should contain a JSON object with suites field.
        Assert.Contains("\"suites\"", output);
        Assert.Contains("\"total_documents\"", output);
    }
}
