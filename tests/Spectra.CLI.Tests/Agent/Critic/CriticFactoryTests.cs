using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Critic;

/// <summary>
/// Tests for CriticFactory after Spec 058 retired the in-process critic. <see cref="CriticFactory.TryCreate"/>
/// always reports the in-process critic unavailable (ProviderName "subagent"); the critic of record
/// runs as the spectra-critic subagent. <see cref="CriticFactory.IsSupported"/> is retained as a
/// canonical-set check.
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
    public void TryCreate_NullConfig_ReportsSubagent()
    {
        var result = CriticFactory.TryCreate(null);

        Assert.False(result.Success);
        Assert.Null(result.Critic);
        Assert.Equal("subagent", result.ProviderName);
    }

    [Fact]
    public void TryCreate_EnabledConfig_ReportsSubagentUnavailable()
    {
        // Spec 058: the in-process critic is retired; the factory never constructs one.
        var config = new CriticConfig { Enabled = true };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Null(result.Critic);
        Assert.Equal("subagent", result.ProviderName);
        Assert.Contains("subagent", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
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
