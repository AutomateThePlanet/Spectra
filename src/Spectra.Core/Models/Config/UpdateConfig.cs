using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test updates.
/// </summary>
public sealed class UpdateConfig
{
    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; init; } = 30;

    [JsonPropertyName("require_review")]
    public bool RequireReview { get; init; } = true;
}
