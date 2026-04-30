using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Options;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands.Docs;

[Collection("WorkingDirectory")]
public class DocsShowSuiteHandlerTests : IDisposable
{
    private readonly string _testDir;

    public DocsShowSuiteHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-show-suite-{Guid.NewGuid():N}");
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

    private async Task SeedAndIndexAsync(params string[] docPaths)
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

        await RunAsync("docs", "index", "--skip-criteria");
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
    public async Task ShowSuite_KnownSuite_ReturnsSuccess()
    {
        await SeedAndIndexAsync("docs/checkout/a.md");

        var exitCode = await RunAsync("docs", "show-suite", "checkout");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ShowSuite_UnknownSuite_ReturnsError()
    {
        await SeedAndIndexAsync("docs/checkout/a.md");

        var exitCode = await RunAsync("docs", "show-suite", "nonexistent");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ShowSuite_NoManifest_ReturnsError()
    {
        var config = SpectraConfig.Default;
        await File.WriteAllTextAsync(
            Path.Combine(_testDir, "spectra.config.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        var exitCode = await RunAsync("docs", "show-suite", "anything");

        Assert.Equal(1, exitCode);
    }
}
