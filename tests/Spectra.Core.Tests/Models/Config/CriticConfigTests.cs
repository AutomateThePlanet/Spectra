using Spectra.Core.Models.Config;

namespace Spectra.Core.Tests.Models.Config;

public class CriticConfigTests
{
    [Fact]
    public void CriticConfig_DefaultValues()
    {
        var config = new CriticConfig();

        Assert.False(config.Enabled);
        Assert.Null(config.Provider);
        Assert.Null(config.Model);
        Assert.Null(config.ApiKeyEnv);
        Assert.Null(config.BaseUrl);
        // v1.43.0: bumped default from 30 to 120 to match the prior hardcoded
        // 2-minute runtime behavior in CopilotCritic (the 30-sec default was a
        // dead value that the runtime ignored).
        Assert.Equal(120, config.TimeoutSeconds);
    }

    [Fact]
    public void CriticConfig_IsValid_WhenDisabled()
    {
        var config = new CriticConfig { Enabled = false };

        Assert.True(config.IsValid());
    }

    [Fact]
    public void CriticConfig_IsValid_WhenEnabledWithProvider()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "google"
        };

        Assert.True(config.IsValid());
    }

    [Fact]
    public void CriticConfig_IsInvalid_WhenEnabledWithoutProvider()
    {
        var config = new CriticConfig { Enabled = true };

        Assert.False(config.IsValid());
    }

    [Theory]
    [InlineData("google", "gemini-2.0-flash")]
    [InlineData("openai", "gpt-4o-mini")]
    [InlineData("anthropic", "claude-3-5-haiku-latest")]
    [InlineData("github", "gpt-4o-mini")]
    [InlineData("unknown", "gpt-4o-mini")]
    public void CriticConfig_GetEffectiveModel_ReturnsDefaultForProvider(string provider, string expectedModel)
    {
        var config = new CriticConfig { Provider = provider };

        Assert.Equal(expectedModel, config.GetEffectiveModel());
    }

    [Fact]
    public void CriticConfig_GetEffectiveModel_PrefersExplicitModel()
    {
        var config = new CriticConfig
        {
            Provider = "google",
            Model = "gemini-1.5-pro"
        };

        Assert.Equal("gemini-1.5-pro", config.GetEffectiveModel());
    }

    [Theory]
    // Spec 039: canonical providers
    [InlineData("github-models", "GITHUB_TOKEN")]
    [InlineData("azure-openai", "AZURE_OPENAI_API_KEY")]
    [InlineData("azure-anthropic", "AZURE_ANTHROPIC_API_KEY")]
    [InlineData("openai", "OPENAI_API_KEY")]
    [InlineData("anthropic", "ANTHROPIC_API_KEY")]
    // Legacy fallthroughs (preserved for read-side safety)
    [InlineData("github", "GITHUB_TOKEN")]
    [InlineData("google", "GOOGLE_API_KEY")]
    // Unknown defaults to GITHUB_TOKEN (the default provider's key) post-039
    [InlineData("unknown", "GITHUB_TOKEN")]
    public void CriticConfig_GetDefaultApiKeyEnv_ReturnsCorrectEnvVar(string provider, string expectedEnvVar)
    {
        var config = new CriticConfig { Provider = provider };

        Assert.Equal(expectedEnvVar, config.GetDefaultApiKeyEnv());
    }

    [Fact]
    public void CriticConfig_CanBeFullyConfigured()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "google",
            Model = "gemini-2.0-flash",
            ApiKeyEnv = "MY_GOOGLE_KEY",
            BaseUrl = "https://custom.api.com",
            TimeoutSeconds = 60
        };

        Assert.True(config.Enabled);
        Assert.Equal("google", config.Provider);
        Assert.Equal("gemini-2.0-flash", config.Model);
        Assert.Equal("MY_GOOGLE_KEY", config.ApiKeyEnv);
        Assert.Equal("https://custom.api.com", config.BaseUrl);
        Assert.Equal(60, config.TimeoutSeconds);
        Assert.True(config.IsValid());
    }
}
