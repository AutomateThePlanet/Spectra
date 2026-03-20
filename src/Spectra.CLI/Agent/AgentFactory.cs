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
            "openai" or "azure-openai" => new OpenAiAgent(provider),
            "anthropic" or "azure-anthropic" => new AnthropicAgent(provider),
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
            "openai" or "azure-openai" => TryCreateOpenAiAgent(provider),
            "anthropic" or "azure-anthropic" => TryCreateAnthropicAgent(provider),
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
        return ["github-models", "openai", "azure-openai", "anthropic", "azure-anthropic", "mock"];
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
            "azure-openai" => GetSimpleAuthStatus(apiKeyEnv ?? "AZURE_OPENAI_API_KEY", "azure-openai"),
            "anthropic" => GetSimpleAuthStatus(apiKeyEnv ?? "ANTHROPIC_API_KEY", "anthropic"),
            "azure-anthropic" => GetSimpleAuthStatus(apiKeyEnv ?? "AZURE_ANTHROPIC_API_KEY", "azure-anthropic"),
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
        var isAzure = IsAzureEndpoint(provider.BaseUrl);
        var providerName = isAzure ? "azure-openai" : "openai";
        var defaultEnv = isAzure ? "AZURE_OPENAI_API_KEY" : "OPENAI_API_KEY";
        var envVar = provider.ApiKeyEnv ?? defaultEnv;
        var apiKey = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrEmpty(apiKey))
        {
            var displayName = isAzure ? "Azure OpenAI" : "OpenAI";
            return AgentCreateResult.Failed(providerName,
                AuthResult.Failure(
                    $"{displayName} API key not found",
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
            return AgentCreateResult.Failed(providerName,
                AuthResult.Failure($"Failed to create agent: {ex.Message}"));
        }
    }

    private static bool IsAzureEndpoint(string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return false;
        return baseUrl.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    private static AgentCreateResult TryCreateAnthropicAgent(ProviderConfig provider)
    {
        var isAzure = IsAzureAnthropicEndpoint(provider.BaseUrl);
        var providerName = isAzure ? "azure-anthropic" : "anthropic";
        var defaultEnv = isAzure ? "AZURE_ANTHROPIC_API_KEY" : "ANTHROPIC_API_KEY";
        var envVar = provider.ApiKeyEnv ?? defaultEnv;
        var apiKey = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrEmpty(apiKey))
        {
            var displayName = isAzure ? "Azure Anthropic" : "Anthropic";
            return AgentCreateResult.Failed(providerName,
                AuthResult.Failure(
                    $"{displayName} API key not found",
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
            return AgentCreateResult.Failed(providerName,
                AuthResult.Failure($"Failed to create agent: {ex.Message}"));
        }
    }

    private static bool IsAzureAnthropicEndpoint(string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return false;
        return baseUrl.Contains(".azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".inference.ai.azure.com", StringComparison.OrdinalIgnoreCase);
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
            "azure-openai" => new[]
            {
                $"Set the {envVar} environment variable",
                "Also set base_url in spectra.config.json to your Azure OpenAI endpoint",
                "",
                "For more information: spectra auth --help"
            },
            "anthropic" => new[]
            {
                $"Set the {envVar} environment variable",
                "",
                "For more information: spectra auth --help"
            },
            "azure-anthropic" => new[]
            {
                $"Set the {envVar} environment variable",
                "Also set base_url in spectra.config.json to your Azure AI endpoint",
                "",
                "For more information: spectra auth --help"
            },
            _ => new[] { $"Set the {envVar} environment variable" }
        };

        return AuthResult.Failure($"{providerName} API key not found", instructions);
    }
}
