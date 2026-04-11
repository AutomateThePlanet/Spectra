using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models.Config;
using Xunit;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 041: verify the default critic model for each provider. Exercises
/// <see cref="CopilotCritic.ModelName"/> which is resolved from
/// <c>GetEffectiveModel</c> when the caller leaves <c>CriticConfig.Model</c>
/// empty.
/// </summary>
public class CopilotCriticDefaultModelTests
{
    [Fact]
    public void DefaultModel_GitHubModels_IsGpt5Mini()
    {
        var critic = new CopilotCritic(new CriticConfig
        {
            Enabled = true,
            Provider = "github-models"
            // Model intentionally null → GetEffectiveModel picks the default.
        });
        Assert.Equal("gpt-5-mini", critic.ModelName);
    }

    [Fact]
    public void DefaultModel_OpenAi_IsGpt5Mini()
    {
        var critic = new CopilotCritic(new CriticConfig
        {
            Enabled = true,
            Provider = "openai"
        });
        Assert.Equal("gpt-5-mini", critic.ModelName);
    }

    [Fact]
    public void DefaultModel_AzureOpenAi_IsGpt5Mini()
    {
        var critic = new CopilotCritic(new CriticConfig
        {
            Enabled = true,
            Provider = "azure-openai"
        });
        Assert.Equal("gpt-5-mini", critic.ModelName);
    }

    [Fact]
    public void DefaultModel_Anthropic_IsClaudeHaiku45()
    {
        var critic = new CopilotCritic(new CriticConfig
        {
            Enabled = true,
            Provider = "anthropic"
        });
        Assert.Equal("claude-haiku-4-5", critic.ModelName);
    }

    [Fact]
    public void ExplicitModel_WinsOverDefault()
    {
        var critic = new CopilotCritic(new CriticConfig
        {
            Enabled = true,
            Provider = "github-models",
            Model = "claude-sonnet-4.5"
        });
        Assert.Equal("claude-sonnet-4.5", critic.ModelName);
    }
}
