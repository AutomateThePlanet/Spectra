using System.Text.Json;
using Spectra.Core.Models.Config;
using Xunit;

namespace Spectra.Core.Tests.Config;

public class DebugConfigTests
{
    [Fact]
    public void Default_DisabledByDefault()
    {
        var config = new DebugConfig();
        Assert.False(config.Enabled);
    }

    [Fact]
    public void Default_LogFilePath()
    {
        var config = new DebugConfig();
        Assert.Equal(".spectra-debug.log", config.LogFile);
    }

    [Fact]
    public void Deserialization_FromJson_RoundTrip()
    {
        const string json = """
            {
              "enabled": true,
              "log_file": "logs/debug.log"
            }
            """;

        var config = JsonSerializer.Deserialize<DebugConfig>(json);
        Assert.NotNull(config);
        Assert.True(config!.Enabled);
        Assert.Equal("logs/debug.log", config.LogFile);
    }

    [Fact]
    public void Deserialization_PartialDebugSection_FillsDefaults()
    {
        const string json = """{"enabled": true}""";
        var config = JsonSerializer.Deserialize<DebugConfig>(json);
        Assert.NotNull(config);
        Assert.True(config!.Enabled);
        Assert.Equal(".spectra-debug.log", config.LogFile);
    }

    [Fact]
    public void SpectraConfig_Deserialization_MissingDebugSection_DefaultsToDisabled()
    {
        // SpectraConfig.Debug must default to a new DebugConfig() when the
        // section is absent from the JSON payload — backwards compatibility.
        const string json = """
            {
              "source": { "mode": "local", "local_dir": "docs/" },
              "tests": { "dir": "tests/" },
              "ai": {
                "providers": [
                  { "name": "github-models", "model": "gpt-4o", "enabled": true }
                ]
              }
            }
            """;

        var config = JsonSerializer.Deserialize<SpectraConfig>(json);
        Assert.NotNull(config);
        Assert.NotNull(config!.Debug);
        Assert.False(config.Debug.Enabled);
        Assert.Equal(".spectra-debug.log", config.Debug.LogFile);
    }
}
