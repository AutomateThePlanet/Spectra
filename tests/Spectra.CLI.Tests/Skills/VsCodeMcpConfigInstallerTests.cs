using System.Text.Json;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 068 (FR-013/FR-014/FR-015/FR-018) — <c>.vscode/mcp.json</c> is updated by merge-by-key on
/// <c>servers.spectra</c>: foreign servers and top-level keys are preserved, the merge is idempotent,
/// JSONC is tolerated, and a malformed file fails loud instead of being overwritten.
/// </summary>
public sealed class VsCodeMcpConfigInstallerTests
{
    private static JsonElement Servers(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("servers").Clone();
    }

    private static string[] ServerKeys(string json)
        => Servers(json).EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public void EnsureSpectraServer_FromNull_AddsSpectra()
    {
        var result = VsCodeMcpConfigInstaller.EnsureSpectraServer(null);
        var spectra = Servers(result).GetProperty("spectra");

        Assert.Equal("spectra-mcp", spectra.GetProperty("command").GetString());
        Assert.Equal(".", spectra.GetProperty("args")[0].GetString());
    }

    [Fact]
    public void EnsureSpectraServer_PreservesForeignServer()
    {
        var existing = """
            { "servers": { "bellatrix-desktop-mcp": { "command": "bellatrix-desktop-mcp", "args": ["--mcp"] } } }
            """;

        var keys = ServerKeys(VsCodeMcpConfigInstaller.EnsureSpectraServer(existing));

        Assert.Contains("bellatrix-desktop-mcp", keys);   // foreign key preserved
        Assert.Contains("spectra", keys);                  // own key added
    }

    [Fact]
    public void EnsureSpectraServer_PreservesTopLevelInputs()
    {
        var existing = """{ "servers": {}, "inputs": [ { "id": "x" } ] }""";

        var json = VsCodeMcpConfigInstaller.EnsureSpectraServer(existing);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("inputs", out var inputs));
        Assert.Equal("x", inputs[0].GetProperty("id").GetString());
        Assert.Contains("spectra", ServerKeys(json));
    }

    [Fact]
    public void EnsureSpectraServer_IsIdempotent()
    {
        var once = VsCodeMcpConfigInstaller.EnsureSpectraServer(null);
        var twice = VsCodeMcpConfigInstaller.EnsureSpectraServer(once);

        Assert.Equal(once, twice);
        Assert.Single(ServerKeys(twice), k => k == "spectra");
    }

    [Fact]
    public void EnsureSpectraServer_NoServersObject_CreatesIt()
    {
        var json = VsCodeMcpConfigInstaller.EnsureSpectraServer("""{ "inputs": [] }""");
        Assert.Contains("spectra", ServerKeys(json));
    }

    [Fact]
    public void EnsureSpectraServer_ToleratesJsoncComments()
    {
        var existing = """
            {
              // VS Code allows comments in mcp.json
              "servers": {
                "bellatrix-web-mcp": { "command": "bellatrix-web-mcp", "args": ["--mcp"] },
              },
            }
            """;

        var keys = ServerKeys(VsCodeMcpConfigInstaller.EnsureSpectraServer(existing));

        Assert.Contains("bellatrix-web-mcp", keys);
        Assert.Contains("spectra", keys);
    }

    [Fact]
    public void EnsureSpectraServer_Malformed_ThrowsTyped()
    {
        Assert.Throws<InvalidMcpConfigException>(
            () => VsCodeMcpConfigInstaller.EnsureSpectraServer("{ this is not json"));
    }

    [Fact]
    public void EnsureSpectraServer_NonObjectRoot_ThrowsTyped()
    {
        Assert.Throws<InvalidMcpConfigException>(
            () => VsCodeMcpConfigInstaller.EnsureSpectraServer("[1, 2, 3]"));
    }

    [Fact]
    public async Task EnsureInstalled_FreshDir_WritesFile()
    {
        var dir = NewTempDir();
        try
        {
            var path = await VsCodeMcpConfigInstaller.EnsureInstalledAsync(dir);

            Assert.True(File.Exists(path));
            Assert.EndsWith(Path.Combine(".vscode", "mcp.json"), path);
            Assert.Contains("spectra", ServerKeys(await File.ReadAllTextAsync(path)));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task EnsureInstalled_AlreadyCurrent_IsByteUnchanged()
    {
        var dir = NewTempDir();
        try
        {
            var vscode = Path.Combine(dir, ".vscode");
            Directory.CreateDirectory(vscode);
            var path = Path.Combine(vscode, "mcp.json");

            // Already conformant, WITH a comment and a foreign server — must be left untouched.
            var seeded = """
                {
                  // keep me
                  "servers": {
                    "bellatrix-desktop-mcp": { "command": "bellatrix-desktop-mcp", "args": ["--mcp"] },
                    "spectra": { "command": "spectra-mcp", "args": ["."] }
                  }
                }
                """;
            await File.WriteAllTextAsync(path, seeded);

            await VsCodeMcpConfigInstaller.EnsureInstalledAsync(dir);

            Assert.Equal(seeded, await File.ReadAllTextAsync(path)); // no rewrite, comment preserved
        }
        finally { Cleanup(dir); }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"spectra-mcpmerge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }
}
