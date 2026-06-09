using System.Text.Json;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 068 (SC-003/SC-005/SC-006) — the path <c>spectra init</c> uses to write
/// <c>.vscode/mcp.json</c> (<see cref="VsCodeMcpConfigInstaller.EnsureInstalledAsync"/>, wired into
/// <c>InitHandler.CreateVsCodeMcpConfigAsync</c>) coexists with a peer tool's MCP config: it adds
/// the <c>spectra</c> server while preserving every foreign server, and re-running changes nothing.
/// </summary>
public sealed class InitVsCodeMcpMergeTests
{
    private static string[] ServerKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("servers").EnumerateObject().Select(p => p.Name).ToArray();
    }

    [Fact]
    public async Task Init_WithPreexistingForeignServer_RegistersSpectraAndPreservesForeign()
    {
        var dir = NewTempDir();
        try
        {
            var path = SeedVsCodeMcp(dir, """
                {
                  "servers": {
                    "bellatrix-desktop-mcp": { "command": "bellatrix-desktop-mcp", "args": ["--mcp"] }
                  },
                  "inputs": []
                }
                """);

            await VsCodeMcpConfigInstaller.EnsureInstalledAsync(dir);

            var json = await File.ReadAllTextAsync(path);
            var keys = ServerKeys(json);

            Assert.Contains("bellatrix-desktop-mcp", keys);   // SC-003: foreign key retained
            Assert.Contains("spectra", keys);                  // own server registered
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("inputs", out _)); // top-level key preserved
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task Init_Rerun_IsIdempotent_ForeignServerIntact()
    {
        var dir = NewTempDir();
        try
        {
            var path = SeedVsCodeMcp(dir, """
                {
                  "servers": {
                    "bellatrix-desktop-mcp": { "command": "bellatrix-desktop-mcp", "args": ["--mcp"] }
                  }
                }
                """);

            await VsCodeMcpConfigInstaller.EnsureInstalledAsync(dir);
            var afterFirst = await File.ReadAllTextAsync(path);

            await VsCodeMcpConfigInstaller.EnsureInstalledAsync(dir); // SC-005/SC-006: re-run
            var afterSecond = await File.ReadAllTextAsync(path);

            Assert.Equal(afterFirst, afterSecond);
            var keys = ServerKeys(afterSecond);
            Assert.Contains("bellatrix-desktop-mcp", keys);
            Assert.Single(keys, k => k == "spectra");
        }
        finally { Cleanup(dir); }
    }

    private static string SeedVsCodeMcp(string dir, string json)
    {
        var vscode = Path.Combine(dir, ".vscode");
        Directory.CreateDirectory(vscode);
        var path = Path.Combine(vscode, "mcp.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"spectra-initmcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }
}
