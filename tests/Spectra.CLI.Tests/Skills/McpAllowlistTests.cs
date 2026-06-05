using System.Text.Json;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 057 (FR-005 / SC-005) — the client-side MCP allowlist is merged into
/// <c>.claude/settings.json</c> idempotently, preserving existing entries, and is distinct from the
/// <c>Bash(spectra-mcp:*)</c> entry.
/// </summary>
public sealed class McpAllowlistTests
{
    private static string[] Allow(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    [Fact]
    public void EnsureMcpAllow_FromEmpty_AddsWildcard()
    {
        var result = ClaudeSettingsInstaller.EnsureMcpAllow(null);
        Assert.Contains("mcp__spectra__*", Allow(result));
    }

    [Fact]
    public void EnsureMcpAllow_PreservesExistingEntries()
    {
        var existing = """
            { "permissions": { "allow": ["Bash(spectra-mcp:*)", "Read(*)"] } }
            """;
        var allow = Allow(ClaudeSettingsInstaller.EnsureMcpAllow(existing));

        Assert.Contains("mcp__spectra__*", allow);
        Assert.Contains("Bash(spectra-mcp:*)", allow);   // distinct entry, preserved
        Assert.Contains("Read(*)", allow);
    }

    [Fact]
    public void EnsureMcpAllow_IsIdempotent()
    {
        var once = ClaudeSettingsInstaller.EnsureMcpAllow(null);
        var twice = ClaudeSettingsInstaller.EnsureMcpAllow(once);

        var allow = Allow(twice);
        Assert.Single(allow, a => a == "mcp__spectra__*");
    }

    [Fact]
    public void EnsureMcpAllow_McpEntry_IsDistinctFromBashEntry()
    {
        var allow = Allow(ClaudeSettingsInstaller.EnsureMcpAllow(
            """{ "permissions": { "allow": ["Bash(spectra-mcp:*)"] } }"""));

        Assert.Contains("mcp__spectra__*", allow);
        Assert.DoesNotContain("Bash(mcp__spectra__*)", allow);   // not conflated
        Assert.NotEqual("Bash(spectra-mcp:*)", "mcp__spectra__*");
    }

    [Fact]
    public async Task EnsureInstalled_WritesSettingsFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"spectra-allowlist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = await ClaudeSettingsInstaller.EnsureInstalledAsync(dir);
            Assert.True(File.Exists(path));
            Assert.EndsWith(Path.Combine(".claude", "settings.json"), path);
            Assert.Contains("mcp__spectra__*", Allow(await File.ReadAllTextAsync(path)));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
