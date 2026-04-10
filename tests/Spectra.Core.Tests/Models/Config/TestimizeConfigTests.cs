using System.Text.Json;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Tests.Models.Config;

/// <summary>
/// Spec 038: TestimizeConfig defaults, deserialization, and backward compatibility.
/// </summary>
public class TestimizeConfigTests
{
    [Fact]
    public void Default_Constructed_HasEnabledFalse()
    {
        var c = new TestimizeConfig();
        Assert.False(c.Enabled);
        Assert.Equal("exploratory", c.Mode);
        Assert.Equal("HybridArtificialBeeColony", c.Strategy);
        Assert.Null(c.SettingsFile);
        Assert.NotNull(c.Mcp);
        Assert.Equal("testimize-mcp", c.Mcp.Command);
        Assert.Single(c.Mcp.Args);
        Assert.Equal("--mcp", c.Mcp.Args[0]);
        Assert.Null(c.AbcSettings);
    }

    [Fact]
    public void Deserialize_Full_RoundTrips()
    {
        var json = """
            {
              "enabled": true,
              "mode": "precise",
              "strategy": "Pairwise",
              "settings_file": "testimizeSettings.json",
              "mcp": { "command": "custom-cmd", "args": ["--mcp", "--verbose"] },
              "abc_settings": {
                "total_population_generations": 200,
                "mutation_rate": 0.7,
                "final_population_selection_ratio": 0.6,
                "elite_selection_ratio": 0.4,
                "allow_multiple_invalid_inputs": true,
                "seed": 42
              }
            }
            """;

        var c = JsonSerializer.Deserialize<TestimizeConfig>(json);

        Assert.NotNull(c);
        Assert.True(c.Enabled);
        Assert.Equal("precise", c.Mode);
        Assert.Equal("Pairwise", c.Strategy);
        Assert.Equal("testimizeSettings.json", c.SettingsFile);
        Assert.Equal("custom-cmd", c.Mcp.Command);
        Assert.Equal(2, c.Mcp.Args.Length);
        Assert.NotNull(c.AbcSettings);
        Assert.Equal(200, c.AbcSettings.TotalPopulationGenerations);
        Assert.Equal(0.7, c.AbcSettings.MutationRate);
        Assert.Equal(42, c.AbcSettings.Seed);
        Assert.True(c.AbcSettings.AllowMultipleInvalidInputs);
    }

    [Fact]
    public void Deserialize_Minimal_UsesDefaults()
    {
        var json = """{"enabled": true}""";

        var c = JsonSerializer.Deserialize<TestimizeConfig>(json);

        Assert.NotNull(c);
        Assert.True(c.Enabled);
        Assert.Equal("exploratory", c.Mode);
        Assert.Equal("HybridArtificialBeeColony", c.Strategy);
        Assert.NotNull(c.Mcp);
    }

    [Fact]
    public void SpectraConfig_Default_ContainsTestimizeWithEnabledFalse()
    {
        var config = SpectraConfig.Default;
        Assert.NotNull(config.Testimize);
        Assert.False(config.Testimize.Enabled);
    }

    [Fact]
    public void SpectraConfig_NoTestimizeKey_DefaultsToEnabledFalse()
    {
        // Pre-spec-038 config: no testimize key at all
        var json = """
            {
              "source": {},
              "tests": {},
              "ai": { "providers": [{"name": "copilot", "model": "gpt-4o", "enabled": true}] }
            }
            """;

        var config = JsonSerializer.Deserialize<SpectraConfig>(json);

        Assert.NotNull(config);
        Assert.NotNull(config.Testimize);
        Assert.False(config.Testimize.Enabled);
    }

    [Fact]
    public void SpectraConfig_Default_SerializesTestimizeSection()
    {
        var json = JsonSerializer.Serialize(SpectraConfig.Default);
        Assert.Contains("\"testimize\"", json);
        Assert.Contains("\"enabled\":false", json);
    }
}
