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
        var provider = config?.Providers?.FirstOrDefault()?.Name ?? "mock";

        return provider.ToLowerInvariant() switch
        {
            "github-copilot" or "copilot" => new CopilotAgent(config),
            "mock" => new MockAgent(),
            _ => new MockAgent() // Default to mock for unknown providers
        };
    }

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    public static IReadOnlyList<string> GetAvailableProviders()
    {
        return ["github-copilot", "mock"];
    }
}
