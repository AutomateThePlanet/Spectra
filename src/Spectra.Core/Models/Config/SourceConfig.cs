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

    /// <summary>
    /// Directory containing the v2 documentation index layout
    /// (Spec 040: <c>_manifest.yaml</c>, <c>_checksums.json</c>, and
    /// <c>groups/{suite}.index.md</c>). Defaults to <c>docs/_index</c>.
    /// Treated as a sibling concept to the legacy <see cref="DocIndex"/>
    /// single-file path; both can coexist during migration.
    /// </summary>
    [JsonPropertyName("doc_index_dir")]
    public string DocIndexDir { get; init; } = "docs/_index";

    /// <summary>
    /// Per-path suite identifier overrides. Keys are repo-relative document
    /// paths (forward slashes); values are the suite ID to assign. Consulted
    /// after frontmatter overrides and before the directory-based default.
    /// Spec 040 §3.5 step 2.
    /// </summary>
    [JsonPropertyName("group_overrides")]
    public IReadOnlyDictionary<string, string> GroupOverrides { get; init; } =
        new Dictionary<string, string>();

    [JsonPropertyName("max_file_size_kb")]
    public int MaxFileSizeKb { get; init; } = 50;

    [JsonPropertyName("include_patterns")]
    public IReadOnlyList<string> IncludePatterns { get; init; } = ["**/*.md"];

    [JsonPropertyName("exclude_patterns")]
    public IReadOnlyList<string> ExcludePatterns { get; init; } = ["**/CHANGELOG.md"];
}
