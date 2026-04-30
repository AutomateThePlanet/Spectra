using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for coverage analysis.
/// </summary>
public sealed class CoverageConfig
{
    /// <summary>
    /// Directories to scan for automation files.
    /// Default: ["tests", "test", "spec", "specs", "e2e"]
    /// </summary>
    [JsonPropertyName("automation_dirs")]
    public IReadOnlyList<string> AutomationDirs { get; init; } =
        ["tests", "test", "spec", "specs", "e2e"];

    /// <summary>
    /// File patterns to scan for test ID references.
    /// Default: ["*.cs", "*.ts", "*.js", "*.py", "*.java"]
    /// </summary>
    [JsonPropertyName("file_patterns")]
    public IReadOnlyList<string> FilePatterns { get; init; } =
        ["*.cs", "*.ts", "*.js", "*.py", "*.java"];

    /// <summary>
    /// Regex patterns to extract test IDs from automation files.
    /// Use {id} as placeholder for the test ID pattern (TC-\d{3,}).
    /// Default patterns match common test frameworks.
    /// </summary>
    [JsonPropertyName("attribute_patterns")]
    public IReadOnlyList<string> AttributePatterns { get; init; } =
    [
        // C# attributes: [TestCase("TC-001")]
        @"\[(?:TestCase|Theory|Fact|Test)\s*\(\s*""(TC-\d{3,})""",
        // xUnit InlineData
        @"\[InlineData\s*\([^)]*""(TC-\d{3,})""",
        // Comment markers: // TC-001:
        @"(?://|#)\s*(TC-\d{3,})(?:\s*:|$)",
        // JavaScript/TypeScript: it("TC-001: ...")
        @"(?:it|describe|test)\s*\(\s*['""](?:.*?)?(TC-\d{3,})"
    ];

    /// <summary>
    /// Test ID pattern regex.
    /// Default: "TC-\d{3,}" (matches TC-001, TC-1234, etc.)
    /// </summary>
    [JsonPropertyName("test_id_pattern")]
    public string TestIdPattern { get; init; } = @"TC-\d{3,}";

    /// <summary>
    /// Whether to report orphaned automation files.
    /// Default: true
    /// </summary>
    [JsonPropertyName("report_orphans")]
    public bool ReportOrphans { get; init; } = true;

    /// <summary>
    /// Whether to report broken links.
    /// Default: true
    /// </summary>
    [JsonPropertyName("report_broken_links")]
    public bool ReportBrokenLinks { get; init; } = true;

    /// <summary>
    /// Whether to report link mismatches.
    /// Default: true
    /// </summary>
    [JsonPropertyName("report_mismatches")]
    public bool ReportMismatches { get; init; } = true;

    /// <summary>
    /// Template-based scan patterns for detecting test IDs in automation files.
    /// Use {id} as placeholder for the test ID pattern.
    /// When non-empty, these take priority over AttributePatterns.
    /// </summary>
    [JsonPropertyName("scan_patterns")]
    public IReadOnlyList<string> ScanPatterns { get; init; } =
    [
        "[TestCase(\"{id}\")]",
        "[ManualTestCase(\"{id}\")]",
        "@pytest.mark.manual_test(\"{id}\")",
        "groups = {\"{id}\"}"
    ];

    /// <summary>
    /// File extensions to scan for automation references.
    /// Mapped to glob patterns (e.g., ".cs" → "*.cs").
    /// </summary>
    [JsonPropertyName("file_extensions")]
    public IReadOnlyList<string> FileExtensions { get; init; } = [".cs", ".java", ".py", ".ts"];

    /// <summary>
    /// Path to the requirements definition YAML file.
    /// </summary>
    [JsonPropertyName("requirements_file")]
    public string RequirementsFile { get; init; } = "docs/requirements/_requirements.yaml";

    /// <summary>
    /// Path to the criteria index YAML file.
    /// </summary>
    [JsonPropertyName("criteria_file")]
    public string CriteriaFile { get; init; } = "docs/criteria/_criteria_index.yaml";

    /// <summary>
    /// Directory containing criteria files.
    /// </summary>
    [JsonPropertyName("criteria_dir")]
    public string CriteriaDir { get; init; } = "docs/criteria";

    /// <summary>
    /// Import configuration for criteria.
    /// </summary>
    [JsonPropertyName("criteria_import")]
    public CriteriaImportConfig CriteriaImport { get; init; } = new();

    /// <summary>
    /// Glob patterns whose matched documents are still indexed but whose suites
    /// are flagged <c>skip_analysis: true</c> (Spec 040 §3.6). The list
    /// REPLACES the defaults rather than merging — set to <c>[]</c> to disable
    /// all default exclusions.
    /// </summary>
    /// <remarks>
    /// Phase 3 evaluates patterns via naive segment-matching (good enough for
    /// the default patterns, all of which take the form <c>**/segment/**</c>
    /// or <c>**/segment*</c>). Phase 5 swaps in
    /// <see cref="Microsoft.Extensions.FileSystemGlobbing"/> for full glob
    /// semantics.
    /// </remarks>
    [JsonPropertyName("analysis_exclude_patterns")]
    public IReadOnlyList<string> AnalysisExcludePatterns { get; init; } =
    [
        "**/Old/**",
        "**/old/**",
        "**/legacy/**",
        "**/archive/**",
        "**/release-notes/**",
        "**/CHANGELOG*",
        "**/SUMMARY.md",
    ];

    /// <summary>
    /// Spillover threshold for per-doc index files (Spec 040 §3.7 Phase 5).
    /// When a single suite's <c>tokens_estimated</c> exceeds this value, the
    /// indexer additionally writes per-doc files under
    /// <c>docs/_index/docs/{sanitized-path}.index.md</c>. Default 80,000 leaves
    /// a 16,000-token margin under the default
    /// <c>ai.analysis.max_prompt_tokens</c> (96,000).
    /// </summary>
    [JsonPropertyName("max_suite_tokens")]
    public int MaxSuiteTokens { get; init; } = 80_000;
}

/// <summary>
/// Configuration for criteria import behavior.
/// </summary>
public sealed class CriteriaImportConfig
{
    [JsonPropertyName("default_source_type")]
    public string DefaultSourceType { get; init; } = "manual";

    [JsonPropertyName("auto_split")]
    public bool AutoSplit { get; init; } = true;

    [JsonPropertyName("normalize_rfc2119")]
    public bool NormalizeRfc2119 { get; init; } = true;

    [JsonPropertyName("id_prefix")]
    public string IdPrefix { get; init; } = "AC";
}
