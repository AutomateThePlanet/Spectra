using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Critic;

public class CriticFactoryTests
{
    [Fact]
    public void SupportedProviders_ContainsExpectedProviders()
    {
        Assert.Contains("google", CriticFactory.SupportedProviders);
        Assert.Contains("openai", CriticFactory.SupportedProviders);
        Assert.Contains("anthropic", CriticFactory.SupportedProviders);
        Assert.Contains("github", CriticFactory.SupportedProviders);
    }

    [Fact]
    public void TryCreate_NullConfig_Fails()
    {
        var result = CriticFactory.TryCreate(null);

        Assert.False(result.Success);
        Assert.Null(result.Critic);
    }

    [Fact]
    public void TryCreate_DisabledConfig_Fails()
    {
        var config = new CriticConfig { Enabled = false };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.ErrorMessage ?? "");
    }

    [Fact]
    public void TryCreate_EnabledWithoutProvider_Fails()
    {
        var config = new CriticConfig { Enabled = true };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("provider", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void TryCreate_UnknownProvider_Fails()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "unknown-provider"
        };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("Unknown", result.ErrorMessage ?? "");
    }

    [Theory]
    [InlineData("google")]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("github")]
    public void TryCreate_ValidProvider_NoApiKey_Fails(string provider)
    {
        // Ensure the env var is not set
        var envVar = provider switch
        {
            "google" => "GOOGLE_API_KEY",
            "openai" => "OPENAI_API_KEY",
            "anthropic" => "ANTHROPIC_API_KEY",
            "github" => "GITHUB_TOKEN",
            _ => "API_KEY"
        };

        // Save and clear the env var
        var originalValue = Environment.GetEnvironmentVariable(envVar);
        Environment.SetEnvironmentVariable(envVar, null);

        try
        {
            var config = new CriticConfig
            {
                Enabled = true,
                Provider = provider
            };

            var result = CriticFactory.TryCreate(config);

            Assert.False(result.Success);
            Assert.Contains("key not found", result.ErrorMessage?.ToLowerInvariant() ?? "");
        }
        finally
        {
            // Restore the env var
            Environment.SetEnvironmentVariable(envVar, originalValue);
        }
    }

    [Theory]
    [InlineData("google", true)]
    [InlineData("openai", true)]
    [InlineData("anthropic", true)]
    [InlineData("github", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void IsSupported_ReturnsCorrectResult(string provider, bool expected)
    {
        var result = CriticFactory.IsSupported(provider);

        Assert.Equal(expected, result);
    }
}
