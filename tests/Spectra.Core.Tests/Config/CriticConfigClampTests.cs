using System.Text.Json;
using Spectra.Core.Models.Config;
using Xunit;

namespace Spectra.Core.Tests.Config;

public class CriticConfigClampTests
{
    [Fact]
    public void MaxConcurrent_Default_IsOne()
    {
        var config = new CriticConfig();
        Assert.Equal(1, config.MaxConcurrent);
        Assert.Equal(1, config.GetEffectiveMaxConcurrent());
    }

    [Fact]
    public void GetEffectiveMaxConcurrent_Zero_ClampsToOne()
    {
        var config = new CriticConfig { MaxConcurrent = 0 };
        Assert.Equal(1, config.GetEffectiveMaxConcurrent());
    }

    [Fact]
    public void GetEffectiveMaxConcurrent_Negative_ClampsToOne()
    {
        var config = new CriticConfig { MaxConcurrent = -5 };
        Assert.Equal(1, config.GetEffectiveMaxConcurrent());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void GetEffectiveMaxConcurrent_InRange_PassesThrough(int value)
    {
        var config = new CriticConfig { MaxConcurrent = value };
        Assert.Equal(value, config.GetEffectiveMaxConcurrent());
    }

    [Theory]
    [InlineData(21)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void GetEffectiveMaxConcurrent_AboveTwenty_ClampsToTwenty(int value)
    {
        var config = new CriticConfig { MaxConcurrent = value };
        Assert.Equal(20, config.GetEffectiveMaxConcurrent());
    }

    [Fact]
    public void Json_Roundtrip_PreservesMaxConcurrent()
    {
        var json = """{"enabled":true,"provider":"github-models","max_concurrent":7}""";
        var config = JsonSerializer.Deserialize<CriticConfig>(json);
        Assert.NotNull(config);
        Assert.Equal(7, config!.MaxConcurrent);
    }
}
