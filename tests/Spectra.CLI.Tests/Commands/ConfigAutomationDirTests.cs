using System.Text.Json;
using Spectra.CLI.Commands.Config;

namespace Spectra.CLI.Tests.Commands;

public class ConfigAutomationDirTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalDir;

    public ConfigAutomationDirTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task AddAutomationDir_AddsToConfig()
    {
        await CreateMinimalConfig();

        var handler = new ConfigHandler();
        var result = await handler.AddAutomationDirAsync("../new-tests");

        Assert.Equal(0, result);

        var config = await ReadConfig();
        var dirs = config?["coverage"]?["automation_dirs"];
        Assert.NotNull(dirs);
        Assert.Contains("../new-tests", dirs.AsArray().Select(d => d?.GetValue<string>()));
    }

    [Fact]
    public async Task AddAutomationDir_DuplicateIsIgnored()
    {
        await CreateMinimalConfig();

        var handler = new ConfigHandler();
        await handler.AddAutomationDirAsync("../new-tests");
        await handler.AddAutomationDirAsync("../new-tests"); // duplicate

        var config = await ReadConfig();
        var dirs = config?["coverage"]?["automation_dirs"]?.AsArray()
            .Select(d => d?.GetValue<string>())
            .Where(d => d == "../new-tests")
            .ToList();
        Assert.Single(dirs!);
    }

    [Fact]
    public async Task RemoveAutomationDir_RemovesFromConfig()
    {
        await CreateMinimalConfig();

        var handler = new ConfigHandler();
        await handler.AddAutomationDirAsync("../to-remove");
        var result = await handler.RemoveAutomationDirAsync("../to-remove");

        Assert.Equal(0, result);

        var config = await ReadConfig();
        var dirs = config?["coverage"]?["automation_dirs"]?.AsArray()
            .Select(d => d?.GetValue<string>());
        Assert.DoesNotContain("../to-remove", dirs!);
    }

    [Fact]
    public async Task RemoveAutomationDir_NotFound_ReturnsSuccess()
    {
        await CreateMinimalConfig();

        var handler = new ConfigHandler();
        var result = await handler.RemoveAutomationDirAsync("nonexistent");

        Assert.Equal(0, result); // Warning, not error
    }

    [Fact]
    public async Task ListAutomationDirs_ReturnsSuccess()
    {
        await CreateMinimalConfig();

        var handler = new ConfigHandler();
        var result = await handler.ListAutomationDirsAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AddAutomationDir_NoConfig_ReturnsError()
    {
        // No config file created
        var handler = new ConfigHandler();
        var result = await handler.AddAutomationDirAsync("../tests");

        Assert.Equal(1, result);
    }

    private async Task CreateMinimalConfig()
    {
        var config = new
        {
            source = new { mode = "local", local_dir = "docs/" },
            tests = new { dir = "tests/" },
            ai = new { providers = Array.Empty<object>() },
            coverage = new { automation_dirs = new[] { "tests" } }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "spectra.config.json"), json);
    }

    private async Task<System.Text.Json.Nodes.JsonNode?> ReadConfig()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "spectra.config.json"));
        return System.Text.Json.Nodes.JsonNode.Parse(json);
    }
}
