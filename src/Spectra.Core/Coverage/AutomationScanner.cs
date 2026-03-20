using System.Text.RegularExpressions;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Coverage;

/// <summary>
/// Scans automation files for test ID references using configurable patterns.
/// </summary>
public sealed class AutomationScanner
{
    private readonly string _basePath;
    private readonly IReadOnlyList<string> _searchDirectories;
    private readonly IReadOnlyList<string> _filePatterns;
    private readonly IReadOnlyList<Regex> _attributePatterns;

    /// <summary>
    /// Creates a scanner from CoverageConfig. Uses ScanPatterns if non-empty,
    /// falling back to AttributePatterns. Uses FileExtensions if non-empty,
    /// falling back to FilePatterns.
    /// </summary>
    public static AutomationScanner FromConfig(string basePath, CoverageConfig config)
    {
        // Convert ScanPatterns templates to regex
        var patterns = new List<string>();
        if (config.ScanPatterns.Count > 0)
        {
            foreach (var template in config.ScanPatterns)
            {
                var escaped = Regex.Escape(template);
                var regex = escaped.Replace(@"\{id\}", $"({config.TestIdPattern})");
                patterns.Add(regex);
            }
        }

        // Fall back to AttributePatterns if no scan patterns produced
        if (patterns.Count == 0)
        {
            patterns.AddRange(config.AttributePatterns);
        }

        // Use FileExtensions → glob patterns, or fall back to FilePatterns
        var filePatterns = config.FileExtensions.Count > 0
            ? config.FileExtensions.Select(ext => ext.StartsWith('.') ? $"*{ext}" : $"*.{ext}").ToList()
            : config.FilePatterns.ToList();

        return new AutomationScanner(
            basePath,
            config.AutomationDirs,
            filePatterns,
            patterns);
    }

    /// <summary>
    /// Creates a new scanner with default patterns.
    /// </summary>
    public AutomationScanner(string basePath) : this(
        basePath,
        ["tests", "test", "spec", "specs", "e2e"],
        ["*.cs", "*.ts", "*.js", "*.py", "*.java"],
        [
            // C# attributes: [TestCase("TC-001")] or [Theory("TC-001")]
            @"\[(?:TestCase|Theory|Fact|Test)\s*\(\s*""(TC-\d{3,})""",
            // xUnit InlineData: [InlineData("TC-001")]
            @"\[InlineData\s*\([^)]*""(TC-\d{3,})""",
            // Comment markers: // TC-001: or # TC-001:
            @"(?://|#)\s*(TC-\d{3,})(?:\s*:|$)",
            // String literals: "TC-001" in test methods
            @"test[Ii]d\s*[=:]\s*""(TC-\d{3,})""",
            // JavaScript/TypeScript: it("TC-001: ...")
            @"(?:it|describe|test)\s*\(\s*['""](?:.*?)?(TC-\d{3,})",
            // Python: def test_tc_001 or pytest.mark.parametrize with TC-001
            @"def\s+test_tc_(\d{3,})",
            @"@pytest\.mark\.parametrize.*?(TC-\d{3,})"
        ])
    { }

    /// <summary>
    /// Creates a scanner with custom configuration.
    /// </summary>
    public AutomationScanner(
        string basePath,
        IEnumerable<string> searchDirectories,
        IEnumerable<string> filePatterns,
        IEnumerable<string> attributePatterns)
    {
        _basePath = basePath;
        _searchDirectories = searchDirectories.ToList();
        _filePatterns = filePatterns.ToList();
        _attributePatterns = attributePatterns
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.Multiline))
            .ToList();
    }

    /// <summary>
    /// Scans all configured directories for test ID references.
    /// Returns a map of file paths to the test IDs they reference.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, AutomationFileInfo>> ScanAsync(
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, AutomationFileInfo>();

        foreach (var dir in _searchDirectories)
        {
            var fullPath = Path.Combine(_basePath, dir);
            if (!Directory.Exists(fullPath))
            {
                continue;
            }

            await ScanDirectoryAsync(fullPath, results, ct);
        }

        return results;
    }

    /// <summary>
    /// Scans a specific directory recursively.
    /// </summary>
    private async Task ScanDirectoryAsync(
        string directory,
        Dictionary<string, AutomationFileInfo> results,
        CancellationToken ct)
    {
        foreach (var pattern in _filePatterns)
        {
            foreach (var file in Directory.GetFiles(directory, pattern, SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(_basePath, file);
                var info = await ScanFileAsync(file, relativePath, ct);

                if (info.ReferencedTestIds.Count > 0)
                {
                    results[relativePath] = info;
                }
            }
        }
    }

    /// <summary>
    /// Scans a single file for test ID references.
    /// </summary>
    private async Task<AutomationFileInfo> ScanFileAsync(
        string filePath,
        string relativePath,
        CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var references = new List<TestReference>();

        var lineOffsets = GetLineOffsets(content);

        foreach (var pattern in _attributePatterns)
        {
            foreach (Match match in pattern.Matches(content))
            {
                var testId = NormalizeTestId(match);
                if (testId is not null)
                {
                    var lineNumber = GetLineNumber(lineOffsets, match.Index);
                    references.Add(new TestReference(testId, lineNumber));
                }
            }
        }

        return new AutomationFileInfo(
            relativePath,
            references.Select(r => r.TestId).Distinct().ToList(),
            references);
    }

    /// <summary>
    /// Normalizes captured test ID (handles Python underscores).
    /// </summary>
    private static string? NormalizeTestId(Match match)
    {
        // Find the captured group
        for (int i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                var value = match.Groups[i].Value;

                // Handle Python-style: test_tc_001 -> TC-001
                if (Regex.IsMatch(value, @"^\d{3,}$"))
                {
                    return $"TC-{value}";
                }

                // Standard format: TC-001
                if (value.StartsWith("TC-", StringComparison.OrdinalIgnoreCase))
                {
                    return value.ToUpperInvariant();
                }

                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets line offsets for line number calculation.
    /// </summary>
    private static List<int> GetLineOffsets(string content)
    {
        var offsets = new List<int> { 0 };
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                offsets.Add(i + 1);
            }
        }
        return offsets;
    }

    /// <summary>
    /// Gets line number from character offset.
    /// </summary>
    private static int GetLineNumber(List<int> lineOffsets, int charOffset)
    {
        int line = 1;
        foreach (var offset in lineOffsets)
        {
            if (offset > charOffset)
            {
                break;
            }
            line++;
        }
        return line;
    }
}

/// <summary>
/// Information about an automation file's test references.
/// </summary>
public sealed record AutomationFileInfo(
    string FilePath,
    IReadOnlyList<string> ReferencedTestIds,
    IReadOnlyList<TestReference> References);

/// <summary>
/// A reference to a test ID found in a file.
/// </summary>
public sealed record TestReference(string TestId, int LineNumber);
