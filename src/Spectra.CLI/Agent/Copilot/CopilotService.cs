using System.Diagnostics;
using System.Runtime.InteropServices;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Spectra.Core.Models.Config;
using SdkProviderConfig = GitHub.Copilot.SDK.ProviderConfig;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Singleton service for managing the Copilot SDK client.
/// Creates and manages CopilotSessions for AI interactions.
/// </summary>
public sealed class CopilotService : IAsyncDisposable
{
    private static CopilotService? _instance;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private readonly CopilotClient _client;
    private bool _disposed;

    private CopilotService(CopilotClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets or creates the singleton CopilotService instance.
    /// </summary>
    public static async Task<CopilotService> GetInstanceAsync(CancellationToken ct = default)
    {
        if (_instance is not null && !_instance._disposed)
            return _instance;

        await _lock.WaitAsync(ct);
        try
        {
            if (_instance is not null && !_instance._disposed)
                return _instance;

            var options = CreateClientOptions();
            var client = new CopilotClient(options);
            _instance = new CopilotService(client);
            return _instance;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Creates a new session for test generation with the specified configuration.
    ///
    /// v1.46.0: <paramref name="mcpServers"/> accepts a map of MCP server
    /// configurations (keys are server names, values are
    /// <see cref="McpLocalServerConfig"/> or <see cref="McpRemoteServerConfig"/>)
    /// that the Copilot SDK attaches to the session. The SDK handles process
    /// spawn, initialize handshake, framing, tool discovery, and lifecycle —
    /// callers no longer need a custom MCP protocol client.
    /// </summary>
    public async Task<CopilotSession> CreateGenerationSessionAsync(
        SpectraProviderConfig? providerConfig,
        IEnumerable<AIFunction>? tools = null,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, object>? mcpServers = null)
    {
        var config = new SessionConfig
        {
            Model = ProviderMapping.GetModelName(providerConfig),
            Provider = ProviderMapping.MapProvider(providerConfig),
            Streaming = true,
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        if (tools is not null)
        {
            config.Tools = tools.ToList();
        }

        if (mcpServers is not null && mcpServers.Count > 0)
        {
            config.McpServers = new Dictionary<string, object>(mcpServers);
        }

        return await _client.CreateSessionAsync(config, ct);
    }

    /// <summary>
    /// Creates a new session for verification/critic with a fast model.
    /// </summary>
    public async Task<CopilotSession> CreateCriticSessionAsync(
        CriticConfig? criticConfig,
        CancellationToken ct = default)
    {
        // Map critic config to provider config for reuse
        var providerConfig = MapCriticToProvider(criticConfig);

        var config = new SessionConfig
        {
            Model = GetCriticModel(criticConfig),
            Provider = ProviderMapping.MapProvider(providerConfig),
            Streaming = false,
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        return await _client.CreateSessionAsync(config, ct);
    }

    /// <summary>
    /// Lists every tool the Copilot SDK currently exposes for the given model,
    /// including local AIFunctions, built-in skills, and tools advertised by
    /// attached MCP servers. Used by GenerationAgent to write a one-shot
    /// snapshot to .spectra-debug.log so we can verify whether testimize tools
    /// actually reached the agent's tool list at session start.
    /// </summary>
    public async Task<GitHub.Copilot.SDK.Rpc.ToolsListResult> ListSessionToolsAsync(
        string model,
        CancellationToken ct = default)
    {
        return await _client.Rpc.Tools.ListAsync(model, ct);
    }

    /// <summary>
    /// Checks if the Copilot CLI binary is available.
    /// </summary>
    public static (bool Available, string? Error) CheckCliAvailable()
    {
        var cliPath = ResolveCopilotCliPath();
        if (cliPath is null)
        {
            return (false, "Copilot CLI not found. Install with: npm install -g @github/copilot");
        }

        return (true, null);
    }

    /// <summary>
    /// Checks if the Copilot CLI is available and authenticated (full ping).
    /// Use for GitHub Models/Copilot providers. BYOK providers should use CheckCliAvailable().
    /// </summary>
    public static async Task<(bool Available, string? Error)> CheckAvailabilityAsync(
        CancellationToken ct = default)
    {
        var (cliAvailable, cliError) = CheckCliAvailable();
        if (!cliAvailable)
        {
            return (false, cliError);
        }

        try
        {
            var options = CreateClientOptions();
            await using var client = new CopilotClient(options);

            // Try a ping to verify connectivity and auth
            await client.StartAsync();
            var ping = await client.PingAsync("health-check", ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            var cliPath = ResolveCopilotCliPath();
            return (false, $"Copilot SDK error: {ex.Message} (CLI found at: {cliPath})");
        }
    }

    /// <summary>
    /// Checks if a specific provider configuration is valid.
    /// </summary>
    public static (bool Valid, string? Error) ValidateProvider(
        SpectraProviderConfig? config)
    {
        if (config is null)
            return (true, null); // Will use default GitHub Models

        return ProviderMapping.ValidateConfig(config);
    }

    /// <summary>
    /// Creates CopilotClientOptions with the resolved CLI path.
    /// </summary>
    private static CopilotClientOptions CreateClientOptions()
    {
        var options = new CopilotClientOptions
        {
            AutoStart = true,
            UseLoggedInUser = true
        };

        // Resolve the CLI path - critical on Windows where npm creates .cmd shims
        var cliPath = ResolveCopilotCliPath();
        if (cliPath is not null)
        {
            options.CliPath = cliPath;
        }

        return options;
    }

    /// <summary>
    /// Resolves the Copilot CLI path, handling Windows .cmd shim issues.
    /// The SDK uses Process.Start with UseShellExecute=false, which cannot
    /// find .cmd files on Windows. This method resolves the actual path.
    /// </summary>
    internal static string? ResolveCopilotCliPath()
    {
        // 1. Check COPILOT_CLI_PATH environment variable (explicit override)
        var envPath = Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        // 2. On Windows, use `where` to find the CLI (handles .cmd/.exe/.bat)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ResolveOnWindows();
        }

        // 3. On Unix, use `which` to find the CLI
        return ResolveOnUnix();
    }

    private static string? ResolveOnWindows()
    {
        // Try `where copilot` to find all matches
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c where copilot 2>nul",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            // Parse the output - `where` returns one path per line
            var paths = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var path in paths)
            {
                // Prefer .cmd (npm shim) - the SDK handles .cmd on Windows
                // when UseShellExecute is false by running through cmd.exe
                if (path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    return path;
                }
            }

            // Fall back to any match
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch
        {
            // Ignore errors from `where` command
        }

        // Try well-known npm global paths
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] npmPaths =
        [
            Path.Combine(userProfile, ".npm-global", "copilot.cmd"),
            Path.Combine(userProfile, "AppData", "Roaming", "npm", "copilot.cmd"),
            Path.Combine(userProfile, ".npm-global", "copilot"),
            Path.Combine(userProfile, "AppData", "Roaming", "npm", "copilot"),
        ];

        foreach (var path in npmPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? ResolveOnUnix()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "copilot",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
            {
                return output;
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static SpectraProviderConfig? MapCriticToProvider(CriticConfig? criticConfig)
    {
        if (criticConfig is null || !criticConfig.Enabled)
            return null;

        return new SpectraProviderConfig
        {
            Name = criticConfig.Provider ?? "github-models",
            Model = criticConfig.Model ?? "gpt-5-mini",
            BaseUrl = criticConfig.BaseUrl,
            ApiKeyEnv = criticConfig.ApiKeyEnv,
            Enabled = true
        };
    }

    private static string GetCriticModel(CriticConfig? criticConfig)
    {
        if (!string.IsNullOrEmpty(criticConfig?.Model))
            return criticConfig.Model;

        // Spec 041: Default to fast/cheap models for verification. Defaults
        // target the current GitHub Copilot free models where possible and
        // favor cross-architecture verification (GPT critic for a Claude
        // generator and vice versa).
        var provider = criticConfig?.Provider?.ToLowerInvariant() ?? "github-models";
        return provider switch
        {
            "anthropic" or "azure-anthropic" => "claude-haiku-4-5",
            "azure-deepseek" => "DeepSeek-V3-0324",
            "openai" or "azure-openai" => "gpt-5-mini",
            _ => "gpt-5-mini"
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        await _client.DisposeAsync();
    }
}
