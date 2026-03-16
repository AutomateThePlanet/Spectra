using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for profile loading behavior.
/// </summary>
public sealed class ProfileConfig
{
    /// <summary>
    /// Custom repository profile file name (default: spectra.profile.md).
    /// </summary>
    [JsonPropertyName("repository_file")]
    public string? RepositoryFile { get; init; }

    /// <summary>
    /// Custom suite profile file name (default: _profile.md).
    /// </summary>
    [JsonPropertyName("suite_file")]
    public string? SuiteFile { get; init; }

    /// <summary>
    /// Whether to auto-detect and load profiles (default: true).
    /// </summary>
    [JsonPropertyName("auto_load")]
    public bool AutoLoad { get; init; } = true;

    /// <summary>
    /// Whether to validate profiles on load (default: true).
    /// </summary>
    [JsonPropertyName("validate_on_load")]
    public bool ValidateOnLoad { get; init; } = true;

    /// <summary>
    /// Whether to include profile in validation command (default: true).
    /// </summary>
    [JsonPropertyName("include_in_validation")]
    public bool IncludeInValidation { get; init; } = true;
}
