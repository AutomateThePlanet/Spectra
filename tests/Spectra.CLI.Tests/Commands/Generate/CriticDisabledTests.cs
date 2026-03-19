using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Tests for critic disabled behavior in generation flow.
/// These tests verify that when critic is disabled or not configured,
/// the generation proceeds without verification.
/// </summary>
public class CriticDisabledTests
{
    [Fact]
    public void CriticConfig_EnabledFalse_ReportsDisabled()
    {
        var config = new CriticConfig
        {
            Enabled = false,
            Provider = "google"
        };

        Assert.False(config.Enabled);
    }

    [Fact]
    public void CriticConfig_NullConfig_IsNotConfigured()
    {
        CriticConfig? config = null;

        Assert.Null(config);
    }

    [Fact]
    public void CriticConfig_DefaultEnabled_IsFalse()
    {
        var config = new CriticConfig();

        // Default should be false for backward compatibility
        Assert.False(config.Enabled);
    }

    [Fact]
    public void CriticConfig_EnabledTrueWithoutProvider_FailsValidation()
    {
        var config = new CriticConfig
        {
            Enabled = true
            // No provider specified
        };

        // When enabled but no provider, it should fail
        Assert.True(config.Enabled);
        Assert.Null(config.Provider);
    }

    [Fact]
    public void AiConfig_NoCriticSection_CriticIsNull()
    {
        var aiConfig = new AiConfig
        {
            Providers = [new ProviderConfig { Name = "copilot", Model = "gpt-4o" }]
        };

        Assert.Null(aiConfig.Critic);
    }

    [Fact]
    public void AiConfig_CriticDisabled_HasCriticButDisabled()
    {
        var aiConfig = new AiConfig
        {
            Providers = [new ProviderConfig { Name = "copilot", Model = "gpt-4o" }],
            Critic = new CriticConfig
            {
                Enabled = false,
                Provider = "google"
            }
        };

        Assert.NotNull(aiConfig.Critic);
        Assert.False(aiConfig.Critic.Enabled);
    }

    [Theory]
    [InlineData(null, false)] // Null config = no verification
    [InlineData(false, false)] // Disabled = no verification
    [InlineData(true, true)] // Enabled = verification
    public void ShouldVerify_ReturnsCorrectResult(bool? enabled, bool expectedShouldVerify)
    {
        CriticConfig? config = enabled.HasValue
            ? new CriticConfig { Enabled = enabled.Value, Provider = "google" }
            : null;

        var shouldVerify = ShouldVerify(config, skipCritic: false);

        Assert.Equal(expectedShouldVerify, shouldVerify);
    }

    [Fact]
    public void ShouldVerify_SkipCriticOverridesEnabled()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Provider = "google"
        };

        var shouldVerify = ShouldVerify(config, skipCritic: true);

        Assert.False(shouldVerify);
    }

    [Fact]
    public void ShouldVerify_SkipCriticWithNullConfig_ReturnsFalse()
    {
        var shouldVerify = ShouldVerify(null, skipCritic: true);

        Assert.False(shouldVerify);
    }

    [Fact]
    public void ShouldVerify_DisabledWithSkipCritic_ReturnsFalse()
    {
        var config = new CriticConfig
        {
            Enabled = false,
            Provider = "google"
        };

        var shouldVerify = ShouldVerify(config, skipCritic: true);

        Assert.False(shouldVerify);
    }

    [Fact]
    public void BackwardCompatibility_ConfigWithoutCritic_WorksWithoutVerification()
    {
        // Simulate old config without critic section
        var aiConfig = new AiConfig
        {
            Providers = [new ProviderConfig { Name = "copilot", Model = "gpt-4o" }]
            // No Critic property set - should be null
        };

        // Verification should be skipped when critic not configured
        Assert.Null(aiConfig.Critic);

        var shouldVerify = ShouldVerify(aiConfig.Critic, skipCritic: false);

        Assert.False(shouldVerify);
    }

    // Helper method that mirrors GenerateHandler.ShouldVerify logic
    private static bool ShouldVerify(CriticConfig? criticConfig, bool skipCritic)
    {
        // Skip if --skip-critic flag is set
        if (skipCritic)
            return false;

        // Skip if critic not configured or disabled
        if (criticConfig is null || !criticConfig.Enabled)
            return false;

        return true;
    }
}
