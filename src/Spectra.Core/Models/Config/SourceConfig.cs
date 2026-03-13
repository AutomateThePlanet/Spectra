using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for source documentation.
/// </summary>
public sealed class SourceConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "local";

    [JsonPropertyName("local_dir")]
    public string LocalDir { get; init; } = "docs/";

    [JsonPropertyName("space_name")]
    public string? SpaceName { get; init; }

    [JsonPropertyName("doc_index")]
    public string? DocIndex { get; init; }

    [JsonPropertyName("max_file_size_kb")]
    public int MaxFileSizeKb { get; init; } = 50;

    [JsonPropertyName("include_patterns")]
    public IReadOnlyList<string> IncludePatterns { get; init; } = ["**/*.md"];

    [JsonPropertyName("exclude_patterns")]
    public IReadOnlyList<string> ExcludePatterns { get; init; } = ["**/CHANGELOG.md"];
}
