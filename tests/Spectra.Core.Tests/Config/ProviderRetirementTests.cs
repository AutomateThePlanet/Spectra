using System.Text.Json;
using Spectra.Core.Config;
using Spectra.Core.Models.Config;
using Xunit;

namespace Spectra.Core.Tests.Config;

/// <summary>
/// Spec 058 (critic-provider retirement + config cleanup). The critic of record is the
/// spectra-critic subagent; <c>ai.critic.model</c> is the only critic selector. The retired keys
/// (<c>ai.fallback_strategy</c>, <c>ai.critic.provider</c>/<c>api_key_env</c>/<c>base_url</c>) are
/// accepted-but-ignored with a non-blocking notice. <c>ai.providers</c> is retained (it still feeds
/// the in-process generator — its removal is Spec 059).
/// </summary>
public class ProviderRetirementTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    // ---------- FR-003 / SC-003: cleaned critic schema validates ----------

    [Fact]
    public void CleanedCriticSchema_ValidatesAndModelIsHonored()
    {
        const string json = """
        {
          "source": { "mode": "local" },
          "tests": { "dir": "test-cases/" },
          "ai": {
            "providers": [ { "name": "github-models", "model": "gpt-4.1", "enabled": true, "priority": 1 } ],
            "critic": { "enabled": true, "model": "claude-sonnet-4-6" }
          }
        }
        """;

        var result = new ConfigLoader().Load(json);

        Assert.True(result.IsSuccess);
        Assert.Equal("claude-sonnet-4-6", result.Value!.Ai.Critic!.Model);
        // No retired keys present → no notice.
        Assert.Empty(ConfigLoader.DetectDeprecatedKeys(json));
    }

    [Fact]
    public void CriticWithoutModel_StillValidates_DefaultApplies()
    {
        const string json = """
        {
          "source": { "mode": "local" },
          "tests": { "dir": "test-cases/" },
          "ai": {
            "providers": [ { "name": "github-models", "model": "gpt-4.1", "enabled": true, "priority": 1 } ],
            "critic": { "enabled": true }
          }
        }
        """;

        var result = new ConfigLoader().Load(json);

        Assert.True(result.IsSuccess);
        // IsValid no longer requires a provider; model is null → CriticModelResolver supplies the default.
        Assert.True(result.Value!.Ai.Critic!.IsValid());
        Assert.Null(result.Value.Ai.Critic.Model);
    }

    // ---------- FR-006: old config ignored-with-notice (non-silent) ----------

    [Fact]
    public void LegacyConfig_WithAllRetiredKeys_ValidatesAndIsDetected()
    {
        const string json = """
        {
          "source": { "mode": "local" },
          "tests": { "dir": "test-cases/" },
          "ai": {
            "providers": [ { "name": "github-models", "model": "gpt-4.1", "enabled": true, "priority": 1 } ],
            "fallback_strategy": "auto",
            "critic": {
              "enabled": true,
              "provider": "github-models",
              "model": "gpt-5-mini",
              "api_key_env": "GITHUB_TOKEN",
              "base_url": "https://example.invalid/"
            }
          }
        }
        """;

        // Still validates (retired keys are ignored, not rejected) — never a silent drop.
        var result = new ConfigLoader().Load(json);
        Assert.True(result.IsSuccess);

        var detected = ConfigLoader.DetectDeprecatedKeys(json);
        Assert.Contains("ai.fallback_strategy", detected);
        Assert.Contains("ai.critic.provider", detected);
        Assert.Contains("ai.critic.api_key_env", detected);
        Assert.Contains("ai.critic.base_url", detected);
        // ai.providers is RETAINED (generator) — it must NOT be flagged as retired.
        Assert.DoesNotContain("ai.providers", detected);
    }

    [Fact]
    public void DetectDeprecatedKeys_CleanConfig_ReturnsEmpty()
    {
        const string json = """
        { "ai": { "providers": [], "critic": { "enabled": true, "model": "claude-sonnet-4-6" } } }
        """;
        Assert.Empty(ConfigLoader.DetectDeprecatedKeys(json));
    }

    [Fact]
    public void DetectDeprecatedKeys_MalformedJson_ReturnsEmpty()
    {
        Assert.Empty(ConfigLoader.DetectDeprecatedKeys("{ not json"));
    }

    // ---------- FR-007: response_format is a verified no-op (absent everywhere) ----------

    [Fact]
    public void NoConfigType_ModelsResponseFormat()
    {
        var configTypes = typeof(AiConfig).Assembly.GetTypes()
            .Where(t => t.Namespace == "Spectra.Core.Models.Config");

        foreach (var type in configTypes)
        {
            foreach (var prop in type.GetProperties())
            {
                var attr = prop.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false)
                    .Cast<System.Text.Json.Serialization.JsonPropertyNameAttribute>()
                    .FirstOrDefault();
                Assert.NotEqual("response_format", attr?.Name);
                Assert.NotEqual("response_format", prop.Name.ToLowerInvariant());
            }
        }
    }

    [Fact]
    public void Config_CarryingResponseFormat_IsIgnored_NotHonored()
    {
        // A stray response_format key must not break the load and must not be modeled.
        const string json = """
        {
          "source": { "mode": "local" },
          "tests": { "dir": "test-cases/" },
          "ai": {
            "providers": [ { "name": "github-models", "model": "gpt-4.1", "enabled": true, "priority": 1 } ],
            "response_format": "json",
            "critic": { "enabled": true, "model": "claude-sonnet-4-6" }
          }
        }
        """;
        var result = new ConfigLoader().Load(json);
        Assert.True(result.IsSuccess);
    }

    // ---------- FR-009 / SC-006: the cleaned demo-config shape validates ----------

    [Theory]
    [InlineData(/* azure-anthropic generator + subagent critic (Spectra_Demo) */ """
        {
          "source": { "mode": "local", "local_dir": "docs/" },
          "tests": { "dir": "test-cases/", "id_prefix": "TC", "id_start": 100 },
          "ai": {
            "providers": [ { "name": "azure-anthropic", "model": "claude-sonnet-4-5", "enabled": true, "priority": 1, "api_key_env": "AZURE_ANTHROPIC_API_KEY" } ],
            "critic": { "enabled": true, "model": "claude-sonnet-4-6", "max_concurrent": 5 }
          },
          "debug": { "enabled": true }
        }
        """)]
    [InlineData(/* github-models generator (AutomateThePlanet_SystemTests) */ """
        {
          "source": { "mode": "local" },
          "tests": { "dir": "test-cases/", "id_prefix": "TC", "id_start": 100 },
          "ai": {
            "providers": [ { "name": "github-models", "model": "claude-sonnet-4.5", "enabled": true, "priority": 1 } ],
            "analysis_timeout_minutes": 3,
            "critic": { "enabled": true, "model": "claude-sonnet-4-6", "max_concurrent": 5 }
          },
          "debug": { "enabled": true }
        }
        """)]
    public void MigratedDemoConfigShape_ValidatesWithNoRetiredKeys(string json)
    {
        var result = new ConfigLoader().Load(json);
        Assert.True(result.IsSuccess);
        Assert.Empty(ConfigLoader.DetectDeprecatedKeys(json));
        // The surviving cost/telemetry levers still bind.
        var config = JsonSerializer.Deserialize<SpectraConfig>(json, Opts)!;
        Assert.NotNull(config.Ai.Critic);
        Assert.Equal("claude-sonnet-4-6", config.Ai.Critic!.Model);
    }
}
