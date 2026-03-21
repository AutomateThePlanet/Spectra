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
    public void SupportedProviders_ContainsExpectedProviders()
    {
        Assert.Contains("github-models", CriticFactory.SupportedProviders);
        Assert.Contains("openai", CriticFactory.SupportedProviders);
        Assert.Contains("anthropic", CriticFactory.SupportedProviders);
        Assert.Contains("google", CriticFactory.SupportedProviders);
        Assert.Contains("azure-openai", CriticFactory.SupportedProviders);
        Assert.Contains("azure-anthropic", CriticFactory.SupportedProviders);
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
    [InlineData("google")]
    [InlineData("azure-openai")]
    [InlineData("azure-anthropic")]
    public void TryCreate_AnyProvider_CreatesCopilotCritic(string provider)
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = provider
        };

        var result = CriticFactory.TryCreate(config);

        // Copilot SDK handles all providers — factory just creates CopilotCritic
        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal(provider, result.ProviderName);
    }

    [Theory]
    [InlineData("github-models", true)]
    [InlineData("openai", true)]
    [InlineData("anthropic", true)]
    [InlineData("google", true)]
    [InlineData("azure-openai", true)]
    [InlineData("azure-anthropic", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void IsSupported_ReturnsCorrectResult(string provider, bool expected)
    {
        var result = CriticFactory.IsSupported(provider);

        Assert.Equal(expected, result);
    }
}
