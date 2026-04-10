using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Spec 038: structured result for `spectra testimize check`.
/// The required fields per FR-027 are <see cref="Enabled"/>, <see cref="Installed"/>,
/// and <see cref="Healthy"/>.
/// </summary>
public sealed class TestimizeCheckResult : CommandResult
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("installed")]
    public bool Installed { get; init; }

    [JsonPropertyName("healthy")]
    public bool Healthy { get; init; }

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("strategy")]
    public string? Strategy { get; init; }

    [JsonPropertyName("settings_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SettingsFile { get; init; }

    [JsonPropertyName("settings_file_found")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SettingsFileFound { get; init; }

    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    [JsonPropertyName("install_command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InstallCommand { get; init; }
}
