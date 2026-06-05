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
        // Spec 058: retired keys (provider/api_key_env/base_url) are ignored on read.
        var json = """
            {
              "providers": [
                { "name": "copilot", "model": "gpt-4o", "enabled": true, "priority": 1 }
              ],
              "critic": {
                "enabled": true,
                "model": "claude-sonnet-4-6"
              }
            }
            """;

        var config = JsonSerializer.Deserialize<AiConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Critic);
        Assert.True(config.Critic.Enabled);
        Assert.Equal("claude-sonnet-4-6", config.Critic.Model);
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
        // Spec 058: retired provider/api_key_env/base_url keys are ignored; surviving
        // fields (enabled, model, timeout_seconds) round-trip.
        var json = """
            {
              "enabled": true,
              "model": "claude-sonnet-4-6",
              "timeout_seconds": 60
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.True(config.Enabled);
        Assert.Equal("claude-sonnet-4-6", config.Model);
        Assert.Equal(60, config.TimeoutSeconds);
    }

    [Fact]
    public void CriticConfig_WithMinimalFields_UsesDefaults()
    {
        var json = """
            {
              "enabled": true
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.True(config.Enabled);
        Assert.Null(config.Model); // Uses resolver default
        Assert.Equal(120, config.TimeoutSeconds); // v1.43.0: default bumped from 30 to 120
    }

    [Fact]
    public void CriticConfig_RetiredKeys_AreIgnored()
    {
        // Spec 058: legacy configs carrying provider/api_key_env/base_url still load —
        // the unknown keys are simply ignored.
        var json = """
            {
              "enabled": true,
              "provider": "openai",
              "api_key_env": "MY_OPENAI_KEY",
              "base_url": "https://custom.openai.com",
              "model": "claude-sonnet-4-6"
            }
            """;

        var config = JsonSerializer.Deserialize<CriticConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.True(config.Enabled);
        Assert.Equal("claude-sonnet-4-6", config.Model);
    }

    [Fact]
    public void CriticConfig_Disabled_DeserializesCorrectly()
    {
        var json = """
            {
              "enabled": false
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
                  "model": "claude-sonnet-4-6"
                }
              }
            }
            """;

        var config = JsonSerializer.Deserialize<SpectraConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Ai);
        Assert.NotNull(config.Ai.Critic);
        Assert.True(config.Ai.Critic.Enabled);
        Assert.Equal("claude-sonnet-4-6", config.Ai.Critic.Model);
    }
}
