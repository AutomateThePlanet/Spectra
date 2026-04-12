using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configures the optional Testimize integration. When <see cref="Enabled"/>
/// is false (the default) SPECTRA does not invoke TestimizeEngine and
/// generation behaves exactly as it did before spec 038. v1.48.3 replaced
/// the MCP child-process integration with a direct in-process NuGet
/// reference, so the old <c>mcp</c> sub-config (command/args) is gone.
/// </summary>
public sealed class TestimizeConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "exploratory";

    [JsonPropertyName("strategy")]
    public string Strategy { get; init; } = "HybridArtificialBeeColony";

    [JsonPropertyName("settings_file")]
    public string? SettingsFile { get; init; }

    [JsonPropertyName("abc_settings")]
    public TestimizeAbcSettings? AbcSettings { get; init; }
}

public sealed class TestimizeAbcSettings
{
    [JsonPropertyName("total_population_generations")]
    public int TotalPopulationGenerations { get; init; } = 100;

    [JsonPropertyName("mutation_rate")]
    public double MutationRate { get; init; } = 0.6;

    [JsonPropertyName("final_population_selection_ratio")]
    public double FinalPopulationSelectionRatio { get; init; } = 0.5;

    [JsonPropertyName("elite_selection_ratio")]
    public double EliteSelectionRatio { get; init; } = 0.3;

    [JsonPropertyName("allow_multiple_invalid_inputs")]
    public bool AllowMultipleInvalidInputs { get; init; } = false;

    [JsonPropertyName("seed")]
    public int? Seed { get; init; }
}
