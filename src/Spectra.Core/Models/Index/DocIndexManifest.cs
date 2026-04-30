using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Index;

/// <summary>
/// Top-level record of the documentation index (Spec 040 / branch 045).
/// Always loaded into AI prompts; the only index artifact that is.
/// Per-suite content lives in <see cref="DocSuiteEntry.IndexFile"/>;
/// content hashes live in a separate <see cref="ChecksumStore"/>.
/// </summary>
public sealed class DocIndexManifest
{
    /// <summary>
    /// Schema version. v1 = legacy single-file <c>_index.md</c>. v2 = this layout.
    /// </summary>
    [YamlMember(Alias = "version")]
    public int Version { get; set; } = 2;

    [YamlMember(Alias = "generated_at")]
    public DateTimeOffset GeneratedAt { get; set; }

    [YamlMember(Alias = "total_documents")]
    public int TotalDocuments { get; set; }

    [YamlMember(Alias = "total_words")]
    public int TotalWords { get; set; }

    [YamlMember(Alias = "total_tokens_estimated")]
    public int TotalTokensEstimated { get; set; }

    /// <summary>
    /// All suites in the corpus. Sorted by <see cref="DocSuiteEntry.Id"/> (ordinal)
    /// on write. Empty for an empty corpus.
    /// </summary>
    [YamlMember(Alias = "groups")]
    public List<DocSuiteEntry> Groups { get; set; } = new();
}
