using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for behavior analysis settings.
/// </summary>
public sealed class AnalysisConfig
{
    [JsonPropertyName("categories")]
    public IReadOnlyList<CategoryDefinition> Categories { get; init; } = [];
}
