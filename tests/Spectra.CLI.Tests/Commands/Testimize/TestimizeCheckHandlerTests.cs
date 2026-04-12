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
    public async Task Check_NoConfig_ReportsDisabledAndNotReady()
    {
        // v1.48.3: the `installed` field now reflects whether the Testimize
        // NuGet assembly is loadable (which it always is, because Spectra.CLI
        // has a compile-time PackageReference to it). `healthy` now means
        // "enabled AND loadable" — disabled ⇒ healthy=false. The old
        // meaning (MCP child process spawned cleanly) is gone.
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
            // Assembly is always present in the process when Spectra.CLI runs.
            Assert.True(doc.RootElement.GetProperty("installed").GetBoolean());
            // Healthy is gated on Enabled=true, so disabled ⇒ healthy=false.
            Assert.False(doc.RootElement.GetProperty("healthy").GetBoolean());
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
