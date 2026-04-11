using GitHub.Copilot.SDK;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Builds <see cref="McpLocalServerConfig"/> instances for the Copilot SDK's
/// native MCP integration (Spec 046 / v1.46.0). Replaces the custom
/// <c>TestimizeMcpClient</c> which spoke a different wire protocol than
/// <c>testimize-mcp</c> actually uses (NDJSON vs Content-Length framing).
///
/// The Copilot SDK (via <see cref="SessionConfig.McpServers"/>) handles:
/// <list type="bullet">
///   <item>Process spawn + lifecycle</item>
///   <item>MCP <c>initialize</c> handshake</item>
///   <item>Both Content-Length and newline-delimited framing variants</item>
///   <item>Tool discovery</item>
///   <item>Cost attribution (<c>AssistantUsageData.Initiator = "mcp-sampling"</c>)</item>
///   <item>OAuth prompts and permission handlers</item>
/// </list>
/// </summary>
public static class McpConfigBuilder
{
    /// <summary>
    /// Per-tool-call timeout for testimize tools, in milliseconds.
    /// 30 seconds matches the old <c>TestimizeMcpClient.CallTimeout</c>.
    /// </summary>
    public const int TestimizeTimeoutMs = 30_000;

    /// <summary>
    /// Builds an <see cref="McpLocalServerConfig"/> for the Testimize MCP
    /// server from user config. Returns <c>null</c> when testimize is
    /// disabled in config — caller should not include it in the
    /// <see cref="SessionConfig.McpServers"/> dictionary in that case.
    /// </summary>
    public static McpLocalServerConfig? BuildTestimizeServer(TestimizeConfig? cfg)
    {
        if (cfg is null || !cfg.Enabled) return null;

        return new McpLocalServerConfig
        {
            Type = "local",
            Command = cfg.Mcp.Command,
            Args = cfg.Mcp.Args?.ToList() ?? new List<string>(),
            Tools = new List<string> { "*" },
            Timeout = TestimizeTimeoutMs,
        };
    }
}
