using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Critic;

/// <summary>
/// Spec 039: critic provider validation aligned with the generator provider list.
/// Covers the canonical 5 providers, legacy alias mapping (github), and the
/// hard-error path (google, unknowns).
/// </summary>
public class CriticFactoryProviderTests
{
    [Fact]
    public void TryCreate_AzureOpenAI_Succeeds()
    {
        var config = new CriticConfig { Enabled = true, Provider = "azure-openai" };

        var result = CriticFactory.TryCreate(config);

        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal("azure-openai", result.ProviderName);
    }

    [Fact]
    public void TryCreate_AzureAnthropic_Succeeds()
    {
        var config = new CriticConfig { Enabled = true, Provider = "azure-anthropic" };

        var result = CriticFactory.TryCreate(config);

        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal("azure-anthropic", result.ProviderName);
    }

    [Fact]
    public void TryCreate_CaseInsensitive_NormalizesToLowercase()
    {
        var config = new CriticConfig { Enabled = true, Provider = "Azure-OpenAI" };

        var result = CriticFactory.TryCreate(config);

        Assert.True(result.Success);
        Assert.Equal("azure-openai", result.ProviderName);
    }

    [Fact]
    public void TryCreate_LegacyGithub_MapsToGithubModels_WithDeprecationWarning()
    {
        var config = new CriticConfig { Enabled = true, Provider = "github" };

        // Capture stderr for the deprecation warning
        var sw = new StringWriter();
        var prev = Console.Error;
        Console.SetError(sw);
        CriticCreateResult result;
        try
        {
            result = CriticFactory.TryCreate(config);
        }
        finally
        {
            Console.SetError(prev);
        }

        Assert.True(result.Success);
        Assert.Equal("github-models", result.ProviderName);

        var stderr = sw.ToString();
        Assert.Contains("deprecated", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("github-models", stderr);
    }

    [Fact]
    public void TryCreate_LegacyGoogle_FailsWithActionableError()
    {
        var config = new CriticConfig { Enabled = true, Provider = "google" };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        // Error must list every canonical provider so the user knows what to switch to
        Assert.Contains("github-models", result.ErrorMessage);
        Assert.Contains("azure-openai", result.ErrorMessage);
        Assert.Contains("azure-anthropic", result.ErrorMessage);
        Assert.Contains("openai", result.ErrorMessage);
        Assert.Contains("anthropic", result.ErrorMessage);
    }

    [Fact]
    public void TryCreate_UnknownProvider_FailsWithSameError()
    {
        var config = new CriticConfig { Enabled = true, Provider = "openia" };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("github-models", result.ErrorMessage);
        Assert.Contains("not supported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreate_EmptyProvider_FallsBackToGithubModels()
    {
        var config = new CriticConfig { Enabled = true, Provider = "" };

        var result = CriticFactory.TryCreate(config);

        Assert.True(result.Success);
        Assert.Equal("github-models", result.ProviderName);
    }

    [Fact]
    public void TryCreate_WhitespaceProvider_FallsBackToGithubModels()
    {
        var config = new CriticConfig { Enabled = true, Provider = "   " };

        var result = CriticFactory.TryCreate(config);

        Assert.True(result.Success);
        Assert.Equal("github-models", result.ProviderName);
    }
}
