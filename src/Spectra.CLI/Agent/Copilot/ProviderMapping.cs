using SdkProviderConfig = GitHub.Copilot.SDK.ProviderConfig;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Maps spectra.config.json provider configuration to Copilot SDK ProviderConfig.
/// </summary>
public static class ProviderMapping
{
    /// <summary>
    /// Maps a Spectra ProviderConfig to a Copilot SDK ProviderConfig.
    /// </summary>
    /// <param name="config">The spectra provider configuration.</param>
    /// <returns>The SDK ProviderConfig, or null for GitHub Models (uses default).</returns>
    public static SdkProviderConfig? MapProvider(SpectraProviderConfig? config)
    {
        if (config is null)
            return null;

        var providerName = config.Name?.ToLowerInvariant() ?? "";

        return providerName switch
        {
            // GitHub Models / Copilot - use default (null)
            "github-models" or "github-copilot" or "copilot" => null,

            // Azure endpoints (OpenAI, Anthropic, or generic Azure AI)
            "azure-openai" or "azure-anthropic" or "azure-ai" or "azure" =>
                CreateAzureProvider(config),

            // Direct Anthropic API
            "anthropic" => CreateAnthropicProvider(config),

            // Direct OpenAI API
            "openai" => CreateOpenAiProvider(config),

            // Unknown - return null to use default
            _ => null
        };
    }

    /// <summary>
    /// Gets the API key from environment variable.
    /// </summary>
    public static string? GetApiKey(SpectraProviderConfig config)
    {
        if (string.IsNullOrEmpty(config.ApiKeyEnv))
            return GetDefaultApiKey(config.Name);

        return Environment.GetEnvironmentVariable(config.ApiKeyEnv);
    }

    /// <summary>
    /// Gets the model name, with provider-specific defaults.
    /// </summary>
    public static string GetModelName(SpectraProviderConfig? config)
    {
        if (!string.IsNullOrEmpty(config?.Model))
            return config.Model;

        var providerName = config?.Name?.ToLowerInvariant() ?? "";
        return providerName switch
        {
            "anthropic" or "azure-anthropic" => "claude-sonnet-4-5-20250514",
            "openai" or "azure-openai" => "gpt-4o",
            "github-models" or "github-copilot" or "copilot" => "gpt-4o",
            _ => "gpt-4o"
        };
    }

    /// <summary>
    /// Determines if the provider is Azure-based.
    /// </summary>
    public static bool IsAzureProvider(SpectraProviderConfig? config)
    {
        if (config is null) return false;

        var providerName = config.Name?.ToLowerInvariant() ?? "";
        if (providerName.StartsWith("azure"))
            return true;

        // Also check base URL for Azure patterns
        return IsAzureEndpoint(config.BaseUrl);
    }

    /// <summary>
    /// Validates that required configuration is present for BYOK.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateConfig(SpectraProviderConfig config)
    {
        var providerName = config.Name?.ToLowerInvariant() ?? "";

        // GitHub Models doesn't require additional config
        if (providerName is "github-models" or "github-copilot" or "copilot")
            return (true, null);

        // BYOK providers require API key
        var apiKey = GetApiKey(config);
        if (string.IsNullOrEmpty(apiKey))
        {
            var envVar = config.ApiKeyEnv ?? GetDefaultEnvVar(providerName);
            return (false, $"API key not found. Set the {envVar} environment variable.");
        }

        // Azure providers require base URL
        if (IsAzureProvider(config) && string.IsNullOrEmpty(config.BaseUrl))
        {
            return (false, "Azure providers require base_url to be set in configuration.");
        }

        return (true, null);
    }

    private static SdkProviderConfig CreateAzureProvider(SpectraProviderConfig config)
    {
        var apiKey = GetApiKey(config);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                $"Azure API key not found. Set the {config.ApiKeyEnv ?? "AZURE_API_KEY"} environment variable.");

        var baseUrl = config.BaseUrl ?? throw new InvalidOperationException("Azure providers require base_url");

        // Strip any API path suffixes the user may have pasted (e.g. /anthropic/v1/messages/).
        baseUrl = StripAzureApiPath(baseUrl);

        var providerName = config.Name?.ToLowerInvariant() ?? "";

        // Azure-hosted Claude uses the Anthropic API format (/anthropic/v1/messages).
        // Azure-hosted OpenAI models use the OpenAI-compatible format (/openai/v1/).
        if (providerName == "azure-anthropic")
        {
            // Type = "anthropic", base URL points to the Azure anthropic endpoint root.
            // The SDK appends /v1/messages itself.
            return new SdkProviderConfig
            {
                Type = "anthropic",
                BaseUrl = baseUrl.TrimEnd('/') + "/anthropic",
                ApiKey = apiKey
            };
        }

        // azure-openai / azure-ai / azure: use OpenAI-compatible gateway
        return new SdkProviderConfig
        {
            Type = "openai",
            BaseUrl = baseUrl.TrimEnd('/') + "/openai/v1/",
            WireApi = "completions",
            ApiKey = apiKey
        };
    }

    /// <summary>
    /// Strips known API path suffixes from Azure endpoint URLs.
    /// e.g. "https://foo.services.ai.azure.com/anthropic/v1/messages/" → "https://foo.services.ai.azure.com"
    /// </summary>
    private static string StripAzureApiPath(string baseUrl)
    {
        // Common API path patterns that users mistakenly include
        string[] apiPathPrefixes = ["/anthropic/", "/openai/", "/v1/"];

        var uri = baseUrl.TrimEnd('/');
        foreach (var prefix in apiPathPrefixes)
        {
            var idx = uri.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                uri = uri[..idx];
                break;
            }
        }

        return uri;
    }

    private static SdkProviderConfig CreateAnthropicProvider(SpectraProviderConfig config)
    {
        var apiKey = GetApiKey(config);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                $"Anthropic API key not found. Set the {config.ApiKeyEnv ?? "ANTHROPIC_API_KEY"} environment variable.");

        return new SdkProviderConfig
        {
            Type = "anthropic",
            BaseUrl = config.BaseUrl ?? "https://api.anthropic.com",
            ApiKey = apiKey
        };
    }

    private static SdkProviderConfig CreateOpenAiProvider(SpectraProviderConfig config)
    {
        var apiKey = GetApiKey(config);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                $"OpenAI API key not found. Set the {config.ApiKeyEnv ?? "OPENAI_API_KEY"} environment variable.");

        return new SdkProviderConfig
        {
            Type = "openai",
            BaseUrl = config.BaseUrl ?? "https://api.openai.com/v1",
            ApiKey = apiKey
        };
    }

    private static bool IsAzureEndpoint(string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return false;

        return baseUrl.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".inference.ai.azure.com", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDefaultApiKey(string? providerName)
    {
        var envVar = GetDefaultEnvVar(providerName);
        return Environment.GetEnvironmentVariable(envVar);
    }

    private static string GetDefaultEnvVar(string? providerName)
    {
        return providerName?.ToLowerInvariant() switch
        {
            "openai" => "OPENAI_API_KEY",
            "anthropic" => "ANTHROPIC_API_KEY",
            "azure-openai" => "AZURE_OPENAI_API_KEY",
            "azure-anthropic" or "azure-ai" or "azure" => "AZURE_API_KEY",
            "github-models" or "github-copilot" or "copilot" => "GITHUB_TOKEN",
            _ => "API_KEY"
        };
    }
}
