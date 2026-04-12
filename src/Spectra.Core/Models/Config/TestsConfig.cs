using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test output.
/// </summary>
public sealed class TestsConfig
{
    [JsonPropertyName("dir")]
    public string Dir { get; init; } = "test-cases/";

    [JsonPropertyName("id_prefix")]
    public string IdPrefix { get; init; } = "TC";

    [JsonPropertyName("id_start")]
    public int IdStart { get; init; } = 100;
}
