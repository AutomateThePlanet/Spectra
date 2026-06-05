using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models.Config;
using Xunit;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 055 (FR-004/FR-008): the provider→default-model switch is gone. <c>ai.critic.model</c> is
/// the single selector; when it is unset, one same-family default applies for EVERY provider (no
/// provider branch). Exercises <see cref="CopilotCritic.ModelName"/>, which now resolves via the
/// single <c>CriticModelResolver</c>. Rewritten from the old Spec 041 per-provider-default tests.
/// </summary>
public class CopilotCriticDefaultModelTests
{
    [Theory]
    [InlineData("github-models")]
    [InlineData("openai")]
    [InlineData("azure-openai")]
    [InlineData("anthropic")]
    [InlineData("azure-anthropic")]
    public void DefaultModel_IsSingleSameFamilyDefault_ForEveryProvider(string provider)
    {
        var critic = new CopilotCritic(new CriticConfig
        {
            Enabled = true,
            Provider = provider
            // Model intentionally null → the single same-family default applies (no provider switch).
        });
        Assert.Equal(Spectra.CLI.Agent.Critic.CriticModelResolver.DefaultCriticModel, critic.ModelName);
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
