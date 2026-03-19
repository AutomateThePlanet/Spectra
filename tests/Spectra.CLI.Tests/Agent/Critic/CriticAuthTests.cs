using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Critic;

public class CriticAuthTests
{
    [Theory]
    [InlineData("google", "GOOGLE_API_KEY")]
    [InlineData("openai", "OPENAI_API_KEY")]
    [InlineData("anthropic", "ANTHROPIC_API_KEY")]
    [InlineData("github", "GITHUB_TOKEN")]
    public void TryCreate_MissingApiKey_ReturnsAuthError(string provider, string envVar)
    {
        // Ensure the env var is not set
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
            Assert.NotEmpty(result.HelpInstructions);
        }
        finally
        {
            // Restore the env var
            Environment.SetEnvironmentVariable(envVar, originalValue);
        }
    }

    [Fact]
    public void TryCreate_MissingApiKey_IncludesHelpInstructions()
    {
        var originalValue = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        Environment.SetEnvironmentVariable("GOOGLE_API_KEY", null);

        try
        {
            var config = new CriticConfig
            {
                Enabled = true,
                Provider = "google"
            };

            var result = CriticFactory.TryCreate(config);

            Assert.False(result.Success);
            Assert.Contains(result.HelpInstructions, h => h.Contains("GOOGLE_API_KEY"));
            Assert.Contains(result.HelpInstructions, h => h.Contains("https://"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", originalValue);
        }
    }

    [Fact]
    public void TryCreate_CustomApiKeyEnv_ChecksCustomEnvVar()
    {
        var customEnvVar = "MY_CUSTOM_CRITIC_KEY";
        var originalValue = Environment.GetEnvironmentVariable(customEnvVar);
        Environment.SetEnvironmentVariable(customEnvVar, null);

        try
        {
            var config = new CriticConfig
            {
                Enabled = true,
                Provider = "openai",
                ApiKeyEnv = customEnvVar
            };

            var result = CriticFactory.TryCreate(config);

            Assert.False(result.Success);
            Assert.Contains(customEnvVar, result.HelpInstructions.FirstOrDefault() ?? "");
        }
        finally
        {
            Environment.SetEnvironmentVariable(customEnvVar, originalValue);
        }
    }

    [Fact]
    public void TryCreate_UnsupportedProvider_ReturnsProviderError()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "unsupported-provider"
        };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("Unknown", result.ErrorMessage ?? "");
        Assert.Contains(result.HelpInstructions, h => h.Contains("google") || h.Contains("Supported"));
    }

    [Fact]
    public void TryCreate_EmptyProvider_ReturnsProviderError()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = ""
        };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("provider", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void TryCreate_NullProvider_ReturnsProviderError()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = null
        };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("provider", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void TryCreate_DisabledConfig_ReturnsDisabledError()
    {
        var config = new CriticConfig
        {
            Enabled = false,
            Provider = "google"
        };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void TryCreate_NullConfig_ReturnsError()
    {
        var result = CriticFactory.TryCreate(null);

        Assert.False(result.Success);
        Assert.Equal("none", result.ProviderName);
    }

    [Theory]
    [InlineData("google", "GOOGLE_API_KEY")]
    [InlineData("openai", "OPENAI_API_KEY")]
    [InlineData("anthropic", "ANTHROPIC_API_KEY")]
    [InlineData("github", "GITHUB_TOKEN")]
    public void TryCreate_WithApiKey_ReturnsSuccess(string provider, string envVar)
    {
        var originalValue = Environment.GetEnvironmentVariable(envVar);
        Environment.SetEnvironmentVariable(envVar, "test-api-key-12345");

        try
        {
            var config = new CriticConfig
            {
                Enabled = true,
                Provider = provider
            };

            var result = CriticFactory.TryCreate(config);

            Assert.True(result.Success);
            Assert.NotNull(result.Critic);
            Assert.Equal(provider, result.ProviderName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, originalValue);
        }
    }

    [Fact]
    public void CriticCreateResult_Succeeded_HasCorrectProperties()
    {
        var mockCritic = new MockCritic();
        var result = CriticCreateResult.Succeeded(mockCritic, "test-provider");

        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal("test-provider", result.ProviderName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void CriticCreateResult_Failed_HasCorrectProperties()
    {
        var result = CriticCreateResult.Failed("test-provider", "Test error", "Help 1", "Help 2");

        Assert.False(result.Success);
        Assert.Null(result.Critic);
        Assert.Equal("test-provider", result.ProviderName);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Equal(2, result.HelpInstructions.Count);
    }

    private sealed class MockCritic : ICriticRuntime
    {
        public string ModelName => "mock-model";

        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<Spectra.Core.Models.Grounding.VerificationResult> VerifyTestAsync(
            Spectra.Core.Models.TestCase test,
            IReadOnlyList<SourceDocument> sources,
            CancellationToken ct) =>
            Task.FromResult(new Spectra.Core.Models.Grounding.VerificationResult
            {
                Verdict = Spectra.Core.Models.Grounding.VerificationVerdict.Grounded,
                Score = 1.0,
                CriticModel = ModelName,
                Findings = []
            });
    }
}
