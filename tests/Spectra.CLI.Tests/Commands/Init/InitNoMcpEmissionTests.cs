using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 070 (US2 / FR-004/FR-005/FR-006, SC-002) — <c>spectra init</c> emits no SPECTRA MCP wiring: no
/// <c>.vscode/mcp.json</c> is written and no <c>mcp__spectra__*</c> allowlist entry is added to
/// <c>.claude/settings.json</c>. A peer tool's pre-existing <c>.vscode/mcp.json</c> is left untouched
/// (init no longer writes the file at all).
/// </summary>
[Collection("WorkingDirectory")]
public class InitNoMcpEmissionTests : IDisposable
{
    private readonly string _testDir;
    private readonly ILogger<InitHandler> _logger;

    public InitNoMcpEmissionTests()
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
    public async Task Init_DoesNotCreateVsCodeMcpJson()
    {
        var handler = new InitHandler(_logger, _testDir);

        Assert.Equal(ExitCodes.Success, await handler.HandleAsync(force: false));

        Assert.False(File.Exists(Path.Combine(_testDir, ".vscode", "mcp.json")),
            "init must not write .vscode/mcp.json");
    }

    [Fact]
    public async Task Init_DoesNotWriteMcpAllowlistEntry()
    {
        var handler = new InitHandler(_logger, _testDir);

        await handler.HandleAsync(force: false);

        var settingsPath = Path.Combine(_testDir, ".claude", "settings.json");
        if (File.Exists(settingsPath))
        {
            var content = await File.ReadAllTextAsync(settingsPath);
            Assert.DoesNotContain("mcp__spectra__", content);
        }
        // If the file does not exist at all, the allowlist is trivially absent.
    }

    [Fact]
    public async Task Init_LeavesPeerVsCodeMcpJsonUntouched()
    {
        var vscodeDir = Path.Combine(_testDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var peerPath = Path.Combine(vscodeDir, "mcp.json");
        const string peer = """
            {
              "servers": {
                "bellatrix-desktop-mcp": { "command": "bellatrix-desktop-mcp", "args": ["--mcp"] }
              },
              "inputs": []
            }
            """;
        await File.WriteAllTextAsync(peerPath, peer);

        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        // Byte-identical: SPECTRA neither merged into nor removed the peer file.
        Assert.Equal(peer, await File.ReadAllTextAsync(peerPath));
    }
}
