using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// Factory for creating AI agent instances.
/// </summary>
public static class AgentFactory
{
    /// <summary>
    /// Creates an agent based on configuration.
    /// </summary>
    public static IAgentRuntime Create(AiConfig? config)
    {
        // Get the first enabled provider or default to mock
        var providerConfig = config?.Providers?.FirstOrDefault(p => p.Enabled);
        if (providerConfig is null)
        {
            return new MockAgent();
        }

        return CreateFromProvider(providerConfig);
    }

    /// <summary>
    /// Creates an agent from a specific provider configuration.
    /// </summary>
    public static IAgentRuntime CreateFromProvider(ProviderConfig provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return provider.Name.ToLowerInvariant() switch
        {
            "github-models" or "github-copilot" or "copilot" => new GitHubModelsAgent(provider),
            "openai" => new OpenAiAgent(provider),
            "anthropic" => new AnthropicAgent(provider),
            "mock" => new MockAgent(),
            _ => new MockAgent() // Default to mock for unknown providers
        };
    }

    /// <summary>
    /// Tries to create an agent, returning null if initialization fails.
    /// </summary>
    public static IAgentRuntime? TryCreateFromProvider(ProviderConfig provider)
    {
        try
        {
            return CreateFromProvider(provider);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    public static IReadOnlyList<string> GetAvailableProviders()
    {
        return ["github-models", "openai", "anthropic", "mock"];
    }
}
