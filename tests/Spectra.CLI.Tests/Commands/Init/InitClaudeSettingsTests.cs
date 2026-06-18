using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Verifies that <c>spectra init</c> emits a COMMITTED <c>.claude/settings.json</c> whose
/// <c>permissions.allow</c> pre-approves the spectra CLI and .spectra/ scratch writes.
/// Idempotent: re-running init merges without duplicating entries; existing settings preserved.
/// No MCP allowlist (Spec 070 GUARD).
/// </summary>
[Collection("WorkingDirectory")]
public class InitClaudeSettingsTests : IDisposable
{
    private readonly string _testDir;
    private readonly ILogger<InitHandler> _logger;

    public InitClaudeSettingsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "spectra-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task Init_EmitsClaudeSettingsJson()
    {
        var handler = new InitHandler(_logger, _testDir);

        Assert.Equal(ExitCodes.Success, await handler.HandleAsync(force: false));

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "settings.json")),
            ".claude/settings.json should be emitted by spectra init");
    }

    [Fact]
    public async Task Init_SettingsJson_ContainsBashSpectraPermission()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var allow = await ReadAllowArrayAsync();
        Assert.Contains("Bash(spectra *)", allow);
    }

    [Fact]
    public async Task Init_SettingsJson_ContainsWriteSpectraScratchPermission()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var allow = await ReadAllowArrayAsync();
        Assert.Contains("Write(.spectra/**)", allow);
    }

    [Fact]
    public async Task Init_SettingsJson_ContainsEditSpectraScratchPermission()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var allow = await ReadAllowArrayAsync();
        Assert.Contains("Edit(.spectra/**)", allow);
    }

    [Fact]
    public async Task Init_SettingsJson_HasNoMcpEntry()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var content = await File.ReadAllTextAsync(Path.Combine(_testDir, ".claude", "settings.json"));
        Assert.DoesNotContain("mcp__spectra__", content);
        Assert.DoesNotContain("mcp.json", content);
    }

    [Fact]
    public async Task Init_SettingsJson_IsIdempotent_NoDuplicateEntries()
    {
        var handler = new InitHandler(_logger, _testDir);

        // First init
        await handler.HandleAsync(force: false);

        // Second init (force to bypass config-exists guard)
        await handler.HandleAsync(force: true);

        var allow = await ReadAllowArrayAsync();
        Assert.Equal(allow.Distinct(StringComparer.Ordinal).ToList(), allow);
        Assert.Single(allow, e => e == "Bash(spectra *)");
        Assert.Single(allow, e => e == "Write(.spectra/**)");
        Assert.Single(allow, e => e == "Edit(.spectra/**)");
    }

    [Fact]
    public async Task Init_SettingsJson_MergesWithExisting_PreservesOtherSettings()
    {
        // Pre-seed a settings.json with a colleague's existing entry
        var claudeDir = Path.Combine(_testDir, ".claude");
        Directory.CreateDirectory(claudeDir);
        var settingsPath = Path.Combine(claudeDir, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "permissions": {
                "allow": ["Bash(git *)"]
              },
              "someOtherKey": "preserved"
            }
            """);

        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var content = await File.ReadAllTextAsync(settingsPath);
        var root = JsonNode.Parse(content)!.AsObject();

        // Existing peer entry preserved
        var allow = await ReadAllowArrayAsync();
        Assert.Contains("Bash(git *)", allow);

        // Spectra entries added
        Assert.Contains("Bash(spectra *)", allow);
        Assert.Contains("Write(.spectra/**)", allow);

        // Unrelated key untouched
        Assert.Equal("preserved", root["someOtherKey"]?.GetValue<string>());
    }

    [Fact]
    public async Task Init_SettingsJson_MergesWithExisting_DoesNotDuplicateExistingSpectraEntry()
    {
        // Pre-seed a settings.json that already has one of our entries
        var claudeDir = Path.Combine(_testDir, ".claude");
        Directory.CreateDirectory(claudeDir);
        var settingsPath = Path.Combine(claudeDir, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "permissions": {
                "allow": ["Bash(spectra *)"]
              }
            }
            """);

        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var allow = await ReadAllowArrayAsync();
        Assert.Single(allow, e => e == "Bash(spectra *)");
    }

    [Fact]
    public async Task Init_SettingsJson_IsInClaudeDir_NotSettingsLocal()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        // Must be the committed file, not settings.LOCAL.json
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "settings.json")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "settings.local.json")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "settings.LOCAL.json")));
    }

    // ---------------------------------------------------------------------------
    private async Task<List<string>> ReadAllowArrayAsync()
    {
        var settingsPath = Path.Combine(_testDir, ".claude", "settings.json");
        var content = await File.ReadAllTextAsync(settingsPath);
        var root = JsonNode.Parse(content)!.AsObject();
        var allow = root["permissions"]!["allow"]!.AsArray();
        return allow.Select(n => n!.GetValue<string>()).ToList();
    }
}
