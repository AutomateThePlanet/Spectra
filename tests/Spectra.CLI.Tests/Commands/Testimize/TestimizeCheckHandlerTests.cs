using System.Text.Json;
using Spectra.CLI.Commands.Testimize;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Commands.Testimize;

/// <summary>
/// Spec 038: spectra testimize check command. Must work in all three states
/// (disabled / enabled-but-missing / enabled-and-installed) and produce
/// JSON output containing the three required fields.
/// </summary>
[Collection("WorkingDirectory")]
public class TestimizeCheckHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalDir;

    public TestimizeCheckHandlerTests()
    {
        _originalDir = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-testimize-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Directory.SetCurrentDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Check_NoConfig_ReportsDisabledNoStartup()
    {
        // Capture stdout
        var sw = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(sw);
        try
        {
            var handler = new TestimizeCheckHandler(OutputFormat.Json);
            var exit = await handler.ExecuteAsync();

            Assert.Equal(0, exit);
            var json = sw.ToString();
            using var doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("installed").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("healthy").GetBoolean());
        }
        finally
        {
            Console.SetOut(prev);
        }
    }

    [Fact]
    public async Task Check_EnabledButBogusCommand_ReportsInstalledFalseWithInstallCommand()
    {
        // Write a config that enables Testimize with a bogus command
        var config = """
            {
              "source": {},
              "tests": {},
              "ai": { "providers": [{"name": "copilot", "model": "gpt-4o", "enabled": true, "priority": 1}] },
              "testimize": {
                "enabled": true,
                "mcp": { "command": "this-tool-does-not-exist-spectra-038", "args": ["--mcp"] }
              }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "spectra.config.json"), config);

        var sw = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(sw);
        try
        {
            var handler = new TestimizeCheckHandler(OutputFormat.Json);
            var exit = await handler.ExecuteAsync();

            Assert.Equal(0, exit);
            var json = sw.ToString();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("enabled").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("installed").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("healthy").GetBoolean());
            Assert.True(doc.RootElement.TryGetProperty("install_command", out var cmd));
            Assert.Contains("dotnet tool install", cmd.GetString());
        }
        finally
        {
            Console.SetOut(prev);
        }
    }

    [Fact]
    public async Task Check_JsonOutput_ContainsRequiredFields()
    {
        var sw = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(sw);
        try
        {
            await new TestimizeCheckHandler(OutputFormat.Json).ExecuteAsync();
            var json = sw.ToString();
            using var doc = JsonDocument.Parse(json);

            // FR-027: at minimum enabled, installed, healthy
            Assert.True(doc.RootElement.TryGetProperty("enabled", out _));
            Assert.True(doc.RootElement.TryGetProperty("installed", out _));
            Assert.True(doc.RootElement.TryGetProperty("healthy", out _));
            // Must also have command/status from base CommandResult
            Assert.Equal("testimize-check", doc.RootElement.GetProperty("command").GetString());
            Assert.Equal("completed", doc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            Console.SetOut(prev);
        }
    }
}
