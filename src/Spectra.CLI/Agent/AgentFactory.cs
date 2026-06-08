using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Services;
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
/// Copilot-SDK auth / provider helpers retained after Spec 059. The in-process generation agent
/// factory (<c>CreateAgentAsync</c>) was removed with the generation inversion — generation now
/// runs on the compile/ingest seam. The auth-status and provider-listing helpers below remain in
/// use by the auth and init commands and by the still-in-process criteria-extraction path.
/// </summary>
public static class AgentFactory
{
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
