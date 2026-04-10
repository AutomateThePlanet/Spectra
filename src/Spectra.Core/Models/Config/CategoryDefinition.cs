using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// A behavior category for test analysis, configurable per project.
/// </summary>
public sealed class CategoryDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
