using Spectra.Core.Models.Config;

namespace Spectra.Core.Tests.Models.Config;

public class CriticConfigTests
{
    [Fact]
    public void CriticConfig_DefaultValues()
    {
        var config = new CriticConfig();

        Assert.False(config.Enabled);
        Assert.Null(config.Model);
        // v1.43.0: bumped default from 30 to 120 to match the prior hardcoded
        // 2-minute runtime behavior (the 30-sec default was a dead value that
        // the runtime ignored).
        Assert.Equal(120, config.TimeoutSeconds);
    }

    [Fact]
    public void CriticConfig_IsValid_WhenDisabled()
    {
        var config = new CriticConfig { Enabled = false };

        Assert.True(config.IsValid());
    }

    [Fact]
    public void CriticConfig_IsValid_WhenEnabled()
    {
        // Spec 058: the critic no longer has a provider — the Claude Code
        // session supplies the runtime — so an enabled critic is always valid.
        var config = new CriticConfig { Enabled = true };

        Assert.True(config.IsValid());
    }

    [Fact]
    public void CriticConfig_CanBeConfigured()
    {
        var config = new CriticConfig
        {
            Enabled = true,
            Model = "claude-sonnet-4-6",
            TimeoutSeconds = 60
        };

        Assert.True(config.Enabled);
        Assert.Equal("claude-sonnet-4-6", config.Model);
        Assert.Equal(60, config.TimeoutSeconds);
        Assert.True(config.IsValid());
    }
}
