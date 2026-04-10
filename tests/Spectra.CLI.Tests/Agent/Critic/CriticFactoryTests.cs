using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Critic;

/// <summary>
/// Tests for CriticFactory after Copilot SDK consolidation.
/// All providers go through CopilotCritic — no per-provider API key checks at factory level.
/// </summary>
public class CriticFactoryTests
{
    [Fact]
    public void SupportedProviders_ContainsCanonicalFiveAfterSpec039()
    {
        // Spec 039: canonical critic provider set is the same as the generator set.
        Assert.Contains("github-models", CriticFactory.SupportedProviders);
        Assert.Contains("openai", CriticFactory.SupportedProviders);
        Assert.Contains("anthropic", CriticFactory.SupportedProviders);
        Assert.Contains("azure-openai", CriticFactory.SupportedProviders);
        Assert.Contains("azure-anthropic", CriticFactory.SupportedProviders);
        Assert.Equal(5, CriticFactory.SupportedProviders.Count);
        // 'google' is no longer canonical (handled as a hard-error alias)
        Assert.DoesNotContain("google", CriticFactory.SupportedProviders);
    }

    [Fact]
    public void TryCreate_NullConfig_Fails()
    {
        var result = CriticFactory.TryCreate(null);

        Assert.False(result.Success);
        Assert.Null(result.Critic);
        Assert.Equal("none", result.ProviderName);
    }

    [Fact]
    public void TryCreate_DisabledConfig_Fails()
    {
        var config = new CriticConfig { Enabled = false };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void TryCreate_EnabledWithProvider_Succeeds()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "github-models"
        };

        var result = CriticFactory.TryCreate(config);

        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal("github-models", result.ProviderName);
    }

    [Fact]
    public void TryCreate_EnabledWithoutProvider_UsesDefault()
    {
        var config = new CriticConfig { Enabled = true };

        var result = CriticFactory.TryCreate(config);

        // After Copilot SDK consolidation, null provider defaults to github-models
        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal("github-models", result.ProviderName);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("azure-openai")]
    [InlineData("azure-anthropic")]
    [InlineData("github-models")]
    public void TryCreate_AnyCanonicalProvider_CreatesCopilotCritic(string provider)
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = provider
        };

        var result = CriticFactory.TryCreate(config);

        // Copilot SDK handles all canonical providers — factory just creates CopilotCritic
        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal(provider, result.ProviderName);
    }

    [Theory]
    [InlineData("github-models", true)]
    [InlineData("openai", true)]
    [InlineData("anthropic", true)]
    [InlineData("azure-openai", true)]
    [InlineData("azure-anthropic", true)]
    [InlineData("github", true)]      // Spec 039: legacy alias is still "supported" (soft-rewritten)
    [InlineData("google", false)]     // Spec 039: hard-rejected
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void IsSupported_ReturnsCorrectResult(string provider, bool expected)
    {
        var result = CriticFactory.IsSupported(provider);

        Assert.Equal(expected, result);
    }
}
