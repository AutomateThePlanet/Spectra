using System.Text.Json;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Config;

public class CriticConfigLoadingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [Fact]
    public void AiConfig_WithCriticSection_DeserializesCorrectly()
    {
        var json = """
            {
              "providers": [
                { "name": "copilot", "model": "gpt-4o", "enabled": true, "priority": 1 }
              ],
              "critic": {
                "enabled": true,
                "provider": "google",
                "model": "gemini-2.0-flash"
              }
            }
            """;

        var config = JsonSerializer.Deserialize<AiConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Critic);
        Assert.True(config.Critic.Enabled);
        Assert.Equal("google", config.Critic.Provider);
        Assert.Equal("gemini-2.0-flash", config.Critic.Model);
    }

    [Fact]
    public void AiConfig_WithoutCriticSection_CriticIsNull()
    {
        var json = """
            {
              "providers": [
                { "name": "copilot", "model": "gpt-4o", "enabled": true, "priority": 1 }
              ]
            }
            """;

        var config = JsonSerializer.Deserialize<AiConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Null(config.Critic);
    }

    [Fact]
    public void CriticConfig_WithAllFields_DeserializesCorrectly()
    {
        var json = """
            {
              "enabled": true,
              "provider": "openai",
              "model": "gpt-4o-mini",
              "api_key_env": "MY_OPENAI_KEY",
              "base_url": "https://custom.openai.com",
              "timeout_seconds": 60
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.True(config.Enabled);
        Assert.Equal("openai", config.Provider);
        Assert.Equal("gpt-4o-mini", config.Model);
        Assert.Equal("MY_OPENAI_KEY", config.ApiKeyEnv);
        Assert.Equal("https://custom.openai.com", config.BaseUrl);
        Assert.Equal(60, config.TimeoutSeconds);
    }

    [Fact]
    public void CriticConfig_WithMinimalFields_UsesDefaults()
    {
        var json = """
            {
              "enabled": true,
              "provider": "anthropic"
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.True(config.Enabled);
        Assert.Equal("anthropic", config.Provider);
        Assert.Null(config.Model); // Uses provider default
        Assert.Null(config.ApiKeyEnv); // Uses provider default
        Assert.Equal(30, config.TimeoutSeconds); // Default
    }

    [Fact]
    public void CriticConfig_Disabled_DeserializesCorrectly()
    {
        var json = """
            {
              "enabled": false,
              "provider": "google"
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.False(config.Enabled);
    }

    [Fact]
    public void SpectraConfig_WithCriticInAi_DeserializesCorrectly()
    {
        var json = """
            {
              "source": { "dir": "docs" },
              "tests": { "dir": "tests" },
              "ai": {
                "providers": [
                  { "name": "copilot", "model": "gpt-4o", "enabled": true, "priority": 1 }
                ],
                "critic": {
                  "enabled": true,
                  "provider": "github",
                  "model": "gpt-4o-mini"
                }
              }
            }
            """;

        var config = JsonSerializer.Deserialize<SpectraConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Ai);
        Assert.NotNull(config.Ai.Critic);
        Assert.True(config.Ai.Critic.Enabled);
        Assert.Equal("github", config.Ai.Critic.Provider);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("github")]
    public void CriticConfig_AllProviders_DeserializeCorrectly(string provider)
    {
        var json = $$"""
            {
              "enabled": true,
              "provider": "{{provider}}"
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Equal(provider, config.Provider);
    }

    [Fact]
    public void CriticConfig_GetEffectiveModel_ReturnsConfiguredModel()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "google",
            Model = "gemini-1.5-pro"
        };

        Assert.Equal("gemini-1.5-pro", config.GetEffectiveModel());
    }

    [Fact]
    public void CriticConfig_GetEffectiveModel_ReturnsDefaultWhenNotConfigured()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "google"
        };

        Assert.Equal("gemini-2.0-flash", config.GetEffectiveModel());
    }

    [Theory]
    [InlineData("google", "GOOGLE_API_KEY")]
    [InlineData("openai", "OPENAI_API_KEY")]
    [InlineData("anthropic", "ANTHROPIC_API_KEY")]
    [InlineData("github", "GITHUB_TOKEN")]
    public void CriticConfig_GetDefaultApiKeyEnv_ReturnsCorrectEnvVar(string provider, string expectedEnv)
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = provider
        };

        Assert.Equal(expectedEnv, config.GetDefaultApiKeyEnv());
    }

    [Fact]
    public void CriticConfig_CustomApiKeyEnv_OverridesDefault()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "google",
            ApiKeyEnv = "MY_CUSTOM_KEY"
        };

        // Custom should be used when specified
        Assert.Equal("MY_CUSTOM_KEY", config.ApiKeyEnv);
    }
}
