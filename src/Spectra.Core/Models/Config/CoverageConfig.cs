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
}
