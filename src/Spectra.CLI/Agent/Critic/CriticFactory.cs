using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Result of attempting to create a critic.
/// </summary>
public sealed record CriticCreateResult
{
    /// <summary>
    /// Gets whether the critic was created successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the critic if creation was successful.
    /// </summary>
    public ICriticRuntime? Critic { get; init; }

    /// <summary>
    /// Gets the provider name that was attempted.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Gets the error message if creation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets authentication help instructions.
    /// </summary>
    public IReadOnlyList<string> HelpInstructions { get; init; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CriticCreateResult Succeeded(ICriticRuntime critic, string providerName) => new()
    {
        Success = true,
        Critic = critic,
        ProviderName = providerName
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static CriticCreateResult Failed(string providerName, string error, params string[] help) => new()
    {
        Success = false,
        ProviderName = providerName,
        ErrorMessage = error,
        HelpInstructions = help
    };
}

/// <summary>
/// Factory for creating critic runtime instances using the Copilot SDK.
/// </summary>
public static class CriticFactory
{
    /// <summary>
    /// Gets the list of supported critic providers.
    /// </summary>
    public static IReadOnlyList<string> SupportedProviders { get; } =
        ["github-models", "openai", "azure-openai", "anthropic", "azure-anthropic", "google"];

    /// <summary>
    /// Tries to create a critic from configuration using the Copilot SDK.
    /// </summary>
    public static CriticCreateResult TryCreate(CriticConfig? config)
    {
        if (config is null || !config.Enabled)
        {
            return CriticCreateResult.Failed("none", "Critic not configured or disabled");
        }

        // Use CopilotCritic for all providers
        var critic = new CopilotCritic(config);
        return CriticCreateResult.Succeeded(critic, config.Provider ?? "github-models");
    }

    /// <summary>
    /// Tries to create a critic asynchronously with availability check.
    /// </summary>
    public static async Task<CriticCreateResult> TryCreateAsync(
        CriticConfig? config,
        CancellationToken ct = default)
    {
        if (config is null || !config.Enabled)
        {
            return CriticCreateResult.Failed("none", "Critic not configured or disabled");
        }

        // Check Copilot availability
        var (available, error) = await CopilotService.CheckAvailabilityAsync(ct);
        if (!available)
        {
            return CriticCreateResult.Failed("copilot-sdk", error ?? "Copilot SDK not available");
        }

        var critic = new CopilotCritic(config);
        return CriticCreateResult.Succeeded(critic, config.Provider ?? "github-models");
    }

    /// <summary>
    /// Checks if a provider is supported.
    /// </summary>
    public static bool IsSupported(string provider) =>
        SupportedProviders.Contains(provider.ToLowerInvariant());
}
