using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 055 (FR-004/FR-008) — <c>ai.critic.model</c> is the single source of truth for the critic
/// model. When set it wins; otherwise one same-family default applies for every provider, with no
/// provider-keyed branch reachable.
/// </summary>
public sealed class CriticModelResolverTests
{
    [Fact]
    public void Resolve_WithModelSet_ReturnsThatModel()
    {
        var model = CriticModelResolver.Resolve(new CriticConfig { Enabled = true, Model = "claude-opus-4-8" });
        Assert.Equal("claude-opus-4-8", model);
    }

    [Theory]
    [InlineData("github-models")]
    [InlineData("openai")]
    [InlineData("azure-openai")]
    [InlineData("anthropic")]
    [InlineData("azure-anthropic")]
    [InlineData("azure-deepseek")]
    public void Resolve_WithNoModel_ReturnsSingleDefault_ForEveryProvider(string provider)
    {
        var model = CriticModelResolver.Resolve(new CriticConfig { Enabled = true, Provider = provider });
        Assert.Equal(CriticModelResolver.DefaultCriticModel, model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithBlankModel_ReturnsDefault(string? model)
    {
        var resolved = CriticModelResolver.Resolve(new CriticConfig { Enabled = true, Model = model });
        Assert.Equal(CriticModelResolver.DefaultCriticModel, resolved);
    }

    [Fact]
    public void Resolve_WithNullConfig_ReturnsDefault()
    {
        Assert.Equal(CriticModelResolver.DefaultCriticModel, CriticModelResolver.Resolve(null));
    }
}
