using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test execution settings.
/// </summary>
public sealed class ExecutionConfig
{
    [JsonPropertyName("copilot_space")]
    public string? CopilotSpace { get; init; }

    [JsonPropertyName("copilot_space_owner")]
    public string? CopilotSpaceOwner { get; init; }
}
