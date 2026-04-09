using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// Result of attempting to create an agent.
/// </summary>
public sealed record AgentCreateResult
{
    /// <summary>
    /// Gets whether the agent was created successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the agent if creation was successful.
    /// </summary>
    public IAgentRuntime? Agent { get; init; }

    /// <summary>
    /// Gets the provider name that was attempted.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Gets the auth result if authentication failed.
    /// </summary>
    public AuthResult? AuthResult { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static AgentCreateResult Succeeded(IAgentRuntime agent) => new()
    {
        Success = true,
        Agent = agent,
        ProviderName = agent.ProviderName
    };

    /// <summary>
    /// Creates a failed result with auth details.
    /// </summary>
    public static AgentCreateResult Failed(string providerName, AuthResult authResult) => new()
    {
        Success = false,
        ProviderName = providerName,
        AuthResult = authResult
    };
}

/// <summary>
/// Factory for creating AI agent instances using the Copilot SDK.
/// </summary>
public static class AgentFactory
{
    /// <summary>
    /// Creates an agent using the Copilot SDK.
    /// This is the primary method for creating agents.
    /// </summary>
    public static async Task<AgentCreateResult> CreateAgentAsync(
        SpectraConfig config,
        string basePath,
        string testsPath,
        Action<string>? onStatus = null,
        CancellationToken ct = default)
    {
        // Get provider config (or use default)
        var providerConfig = config.Ai?.Providers?.FirstOrDefault(p => p.Enabled)
            ?? new ProviderConfig { Name = "github-models", Model = "gpt-4o", Enabled = true };

        var isByok = IsByokProvider(providerConfig);

        if (isByok)
        {
            // BYOK providers only need the CLI binary present — no Copilot auth required.
            // Per SDK docs: BYOK "bypasses GitHub Copilot authentication."
            var (cliAvailable, cliError) = CopilotService.CheckCliAvailable();
            if (!cliAvailable)
            {
                return AgentCreateResult.Failed(providerConfig.Name ?? "copilot-sdk",
                    AuthResult.Failure(
                        cliError ?? "Copilot CLI not available",
                        "Ensure the 'copilot' CLI is installed and in PATH",
                        "Run: npm install -g @github/copilot"));
            }
        }
        else
        {
            // GitHub Models / Copilot requires full auth check (ping)
            var (available, error) = await CopilotService.CheckAvailabilityAsync(ct);
            if (!available)
            {
                return AgentCreateResult.Failed(providerConfig.Name ?? "copilot-sdk",
                    AuthResult.Failure(
                        error ?? "Copilot SDK not available",
                        "Ensure the 'copilot' CLI is installed and in PATH",
                        "Run: copilot --version"));
            }
        }

        // Validate provider (API key, base_url for Azure, etc.)
        var (valid, validationError) = CopilotService.ValidateProvider(providerConfig);
        if (!valid)
        {
            return AgentCreateResult.Failed(providerConfig.Name ?? "copilot-sdk",
                AuthResult.Failure(validationError ?? "Provider validation failed"));
        }

        try
        {
            var agent = new CopilotGenerationAgent(
                providerConfig,
                config,
                basePath,
                testsPath,
                onStatus);

            return AgentCreateResult.Succeeded(agent);
        }
        catch (Exception ex)
        {
            return AgentCreateResult.Failed(providerConfig.Name ?? "copilot-sdk",
                AuthResult.Failure($"Failed to create Copilot agent: {ex.Message}"));
        }
    }

    /// <summary>
    /// Determines if a provider is BYOK (Bring Your Own Key) — i.e. not GitHub Models/Copilot.
    /// BYOK providers bypass GitHub Copilot authentication.
    /// </summary>
    private static bool IsByokProvider(ProviderConfig? config)
    {
        var name = config?.Name?.ToLowerInvariant() ?? "";
        return name is not ("github-models" or "github-copilot" or "copilot" or "");
    }

    /// <summary>
    /// Gets all available provider names supported by the Copilot SDK.
    /// </summary>
    public static IReadOnlyList<string> GetAvailableProviders() =>
        ["github-models", "azure-openai", "azure-deepseek", "azure-anthropic", "openai", "anthropic"];

    /// <summary>
    /// Checks if Copilot SDK is available and configured.
    /// </summary>
    public static async Task<(bool Available, string? Error)> CheckCopilotAvailabilityAsync(
        CancellationToken ct = default)
    {
        return await CopilotService.CheckAvailabilityAsync(ct);
    }

    /// <summary>
    /// Gets authentication status for the Copilot SDK.
    /// </summary>
    public static async Task<AuthResult> GetAuthStatusAsync(CancellationToken ct = default)
    {
        // First check CLI is present
        var (cliAvailable, cliError) = CopilotService.CheckCliAvailable();
        if (!cliAvailable)
        {
            return AuthResult.Failure(
                cliError ?? "Copilot CLI not available",
                "Ensure the 'copilot' CLI is installed and in PATH",
                "Run: npm install -g @github/copilot");
        }

        // Try full auth check (for GitHub Models)
        var (available, error) = await CopilotService.CheckAvailabilityAsync(ct);
        if (available)
        {
            return AuthResult.Success("copilot-sdk", "Copilot CLI (authenticated)");
        }

        // CLI is present but ping failed — still OK for BYOK providers
        return AuthResult.Success("copilot-sdk", "Copilot CLI (BYOK mode — no GitHub auth)");
    }
}
