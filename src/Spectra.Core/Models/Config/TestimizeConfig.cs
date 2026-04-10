using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configures the optional Testimize integration.
/// When <see cref="Enabled"/> is false (the default) SPECTRA does not start
/// the Testimize MCP server and behaves exactly as it did before spec 038.
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

    [JsonPropertyName("mcp")]
    public TestimizeMcpConfig Mcp { get; init; } = new();

    [JsonPropertyName("abc_settings")]
    public TestimizeAbcSettings? AbcSettings { get; init; }
}

public sealed class TestimizeMcpConfig
{
    [JsonPropertyName("command")]
    public string Command { get; init; } = "testimize-mcp";

    [JsonPropertyName("args")]
    public string[] Args { get; init; } = ["--mcp"];
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
