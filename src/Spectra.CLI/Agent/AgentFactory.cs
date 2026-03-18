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
    /// Tries to create an agent with detailed auth information on failure.
    /// </summary>
    public static async Task<AgentCreateResult> TryCreateWithDetailsAsync(
        ProviderConfig provider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var providerName = provider.Name.ToLowerInvariant();

        return providerName switch
        {
            "github-models" or "github-copilot" or "copilot" =>
                await TryCreateGitHubModelsAgentAsync(provider, ct),
            "openai" => TryCreateOpenAiAgent(provider),
            "anthropic" => TryCreateAnthropicAgent(provider),
            "mock" => AgentCreateResult.Succeeded(new MockAgent()),
            _ => AgentCreateResult.Succeeded(new MockAgent())
        };
    }

    /// <summary>
    /// Tries to create an agent based on configuration with detailed results.
    /// </summary>
    public static async Task<AgentCreateResult> TryCreateWithDetailsAsync(
        AiConfig? config,
        CancellationToken ct = default)
    {
        var providerConfig = config?.Providers?.FirstOrDefault(p => p.Enabled);
        if (providerConfig is null)
        {
            return AgentCreateResult.Succeeded(new MockAgent());
        }

        return await TryCreateWithDetailsAsync(providerConfig, ct);
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

    /// <summary>
    /// Gets authentication status for a provider.
    /// </summary>
    public static async Task<AuthResult> GetAuthStatusAsync(
        string providerName,
        string? apiKeyEnv = null,
        CancellationToken ct = default)
    {
        return providerName.ToLowerInvariant() switch
        {
            "github-models" or "github-copilot" or "copilot" =>
                await GitHubCliTokenProvider.TryGetTokenAsync(apiKeyEnv, ct),
            "openai" => GetSimpleAuthStatus(apiKeyEnv ?? "OPENAI_API_KEY", "openai"),
            "anthropic" => GetSimpleAuthStatus(apiKeyEnv ?? "ANTHROPIC_API_KEY", "anthropic"),
            "mock" => AuthResult.Success("mock", "built-in"),
            _ => AuthResult.Failure($"Unknown provider: {providerName}")
        };
    }

    private static async Task<AgentCreateResult> TryCreateGitHubModelsAgentAsync(
        ProviderConfig provider,
        CancellationToken ct)
    {
        var authResult = await GitHubCliTokenProvider.TryGetTokenAsync(provider.ApiKeyEnv, ct);

        if (!authResult.IsAuthenticated)
        {
            return AgentCreateResult.Failed("github-models", authResult);
        }

        try
        {
            var agent = new GitHubModelsAgent(provider, authResult.Token!);
            return AgentCreateResult.Succeeded(agent);
        }
        catch (Exception ex)
        {
            return AgentCreateResult.Failed("github-models",
                AuthResult.Failure($"Failed to create agent: {ex.Message}"));
        }
    }

    private static AgentCreateResult TryCreateOpenAiAgent(ProviderConfig provider)
    {
        var envVar = provider.ApiKeyEnv ?? "OPENAI_API_KEY";
        var apiKey = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrEmpty(apiKey))
        {
            return AgentCreateResult.Failed("openai",
                AuthResult.Failure(
                    "OpenAI API key not found",
                    $"Set the {envVar} environment variable",
                    "",
                    "For more information: spectra auth --help"));
        }

        try
        {
            var agent = new OpenAiAgent(provider);
            return AgentCreateResult.Succeeded(agent);
        }
        catch (Exception ex)
        {
            return AgentCreateResult.Failed("openai",
                AuthResult.Failure($"Failed to create agent: {ex.Message}"));
        }
    }

    private static AgentCreateResult TryCreateAnthropicAgent(ProviderConfig provider)
    {
        var envVar = provider.ApiKeyEnv ?? "ANTHROPIC_API_KEY";
        var apiKey = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrEmpty(apiKey))
        {
            return AgentCreateResult.Failed("anthropic",
                AuthResult.Failure(
                    "Anthropic API key not found",
                    $"Set the {envVar} environment variable",
                    "",
                    "For more information: spectra auth --help"));
        }

        try
        {
            var agent = new AnthropicAgent(provider);
            return AgentCreateResult.Succeeded(agent);
        }
        catch (Exception ex)
        {
            return AgentCreateResult.Failed("anthropic",
                AuthResult.Failure($"Failed to create agent: {ex.Message}"));
        }
    }

    private static AuthResult GetSimpleAuthStatus(string envVar, string providerName)
    {
        var apiKey = Environment.GetEnvironmentVariable(envVar);

        if (!string.IsNullOrEmpty(apiKey))
        {
            return AuthResult.Success(apiKey, $"environment ({envVar})");
        }

        var instructions = providerName switch
        {
            "openai" => new[]
            {
                $"Set the {envVar} environment variable",
                "",
                "For more information: spectra auth --help"
            },
            "anthropic" => new[]
            {
                $"Set the {envVar} environment variable",
                "",
                "For more information: spectra auth --help"
            },
            _ => new[] { $"Set the {envVar} environment variable" }
        };

        return AuthResult.Failure($"{providerName} API key not found", instructions);
    }
}
