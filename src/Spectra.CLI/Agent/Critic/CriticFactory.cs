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
/// Factory for creating critic runtime instances.
/// </summary>
public static class CriticFactory
{
    /// <summary>
    /// Gets the list of supported critic providers.
    /// </summary>
    public static IReadOnlyList<string> SupportedProviders { get; } =
        ["google", "openai", "anthropic", "github"];

    /// <summary>
    /// Tries to create a critic from configuration with detailed error information.
    /// </summary>
    public static CriticCreateResult TryCreate(CriticConfig? config)
    {
        if (config is null || !config.Enabled)
        {
            return CriticCreateResult.Failed("none", "Critic not configured or disabled");
        }

        var provider = config.Provider?.ToLowerInvariant();

        if (string.IsNullOrEmpty(provider))
        {
            return CriticCreateResult.Failed("none",
                "Critic provider not specified",
                "Add 'provider' to the critic configuration in spectra.config.json");
        }

        if (!SupportedProviders.Contains(provider))
        {
            return CriticCreateResult.Failed(provider,
                $"Unknown critic provider: {provider}",
                $"Supported providers: {string.Join(", ", SupportedProviders)}");
        }

        // Get API key
        var envVar = config.ApiKeyEnv ?? config.GetDefaultApiKeyEnv();
        var apiKey = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrEmpty(apiKey))
        {
            return CriticCreateResult.Failed(provider,
                $"{provider} API key not found",
                $"Set the {envVar} environment variable",
                GetProviderHelpUrl(provider));
        }

        try
        {
            var critic = CreateCritic(provider, config, apiKey);
            return CriticCreateResult.Succeeded(critic, provider);
        }
        catch (Exception ex)
        {
            return CriticCreateResult.Failed(provider, $"Failed to create critic: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a critic instance for the given provider.
    /// </summary>
    private static ICriticRuntime CreateCritic(string provider, CriticConfig config, string apiKey)
    {
        return provider switch
        {
            "google" => new GoogleCritic(config, apiKey),
            "openai" => new OpenAiCritic(config, apiKey),
            "anthropic" => new AnthropicCritic(config, apiKey),
            "github" => new GitHubCritic(config, apiKey),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };
    }

    /// <summary>
    /// Gets the help URL for a provider.
    /// </summary>
    private static string GetProviderHelpUrl(string provider) => provider switch
    {
        "google" => "See: https://ai.google.dev/gemini-api/docs/api-key",
        "openai" => "See: https://platform.openai.com/api-keys",
        "anthropic" => "See: https://console.anthropic.com/settings/keys",
        "github" => "See: https://github.com/settings/tokens",
        _ => "See documentation for API key setup"
    };

    /// <summary>
    /// Checks if a provider is supported.
    /// </summary>
    public static bool IsSupported(string provider) =>
        SupportedProviders.Contains(provider.ToLowerInvariant());
}
