using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test validation.
/// </summary>
public sealed class ValidationConfig
{
    [JsonPropertyName("required_fields")]
    public IReadOnlyList<string> RequiredFields { get; init; } = ["id", "priority"];

    [JsonPropertyName("allowed_priorities")]
    public IReadOnlyList<string> AllowedPriorities { get; init; } = ["high", "medium", "low"];

    [JsonPropertyName("max_steps")]
    public int MaxSteps { get; init; } = 20;

    [JsonPropertyName("id_pattern")]
    public string IdPattern { get; init; } = @"^TC-\d{3,}$";
}
