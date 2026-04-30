using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Index;

/// <summary>
/// One suite within the manifest. Holds identity, location, sizing, and analysis flags.
/// Companion file at <c>&lt;doc_index_dir&gt;/&lt;IndexFile&gt;</c> contains the per-doc entries.
/// </summary>
public sealed class DocSuiteEntry
{
    /// <summary>
    /// Stable suite identifier. Matches <see cref="IdRegex"/>: alphanumeric plus
    /// <c>.</c>, <c>_</c>, <c>-</c>. Must not start with <c>.</c> or <c>-</c>.
    /// Filesystem-safe and usable as a <c>--suite</c> CLI argument.
    /// </summary>
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Repo-relative path to the suite's source directory, forward slashes.
    /// </summary>
    [YamlMember(Alias = "path")]
    public string Path { get; set; } = string.Empty;

    [YamlMember(Alias = "document_count")]
    public int DocumentCount { get; set; }

    [YamlMember(Alias = "tokens_estimated")]
    public int TokensEstimated { get; set; }

    /// <summary>
    /// When true, AI analyzers (BehaviorAnalyzer, RequirementsExtractor) exclude
    /// this suite from prompt input by default. Coverage analysis still considers
    /// the suite's documents.
    /// </summary>
    [YamlMember(Alias = "skip_analysis")]
    public bool SkipAnalysis { get; set; }

    /// <summary>
    /// Reason for <see cref="SkipAnalysis"/>. One of <c>pattern</c>, <c>config</c>,
    /// <c>frontmatter</c>, <c>none</c>.
    /// </summary>
    [YamlMember(Alias = "excluded_by")]
    public string ExcludedBy { get; set; } = "none";

    /// <summary>
    /// When <see cref="ExcludedBy"/> is <c>pattern</c>, the matched glob.
    /// Null otherwise (omitted from YAML output).
    /// </summary>
    [YamlMember(Alias = "excluded_pattern", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? ExcludedPattern { get; set; }

    /// <summary>
    /// Path within <c>doc_index_dir</c> to the suite's index file
    /// (e.g., <c>groups/SM_GSG_Topics.index.md</c>).
    /// </summary>
    [YamlMember(Alias = "index_file")]
    public string IndexFile { get; set; } = string.Empty;

    /// <summary>
    /// When non-null and non-empty, lists repo-relative source-document paths
    /// that have per-doc spillover index files (Phase 5 / Spec 041 plumbing).
    /// Null in v2 layouts written before Phase 5.
    /// </summary>
    [YamlMember(Alias = "spillover_files", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public List<string>? SpilloverFiles { get; set; }

    /// <summary>
    /// Validation regex for <see cref="Id"/>. Must not start with <c>.</c> or <c>-</c>.
    /// Frontmatter-supplied IDs MUST match this; others are rejected by
    /// <c>SuiteResolver</c> with a clear error pointing to the offending file.
    /// </summary>
    public static readonly Regex IdRegex = new(
        @"^(?![.\-])[A-Za-z0-9._-]+$",
        RegexOptions.Compiled);
}
