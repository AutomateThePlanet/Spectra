using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Index;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.Data;

/// <summary>
/// Severity level for coverage gaps based on document size/complexity.
/// </summary>
public enum DocGapSeverity
{
    /// <summary>Document &gt; 10KB or &gt; 5 headings.</summary>
    High,
    /// <summary>Document &gt; 5KB or &gt; 2 headings.</summary>
    Medium,
    /// <summary>Default for smaller documents.</summary>
    Low
}

/// <summary>
/// Represents an uncovered documentation area for MCP tool responses.
/// </summary>
public sealed record DocCoverageGap
{
    public required string DocumentPath { get; init; }
    public required string DocumentTitle { get; init; }
    public DocGapSeverity Severity { get; init; }
    public int SizeKb { get; init; }
    public int HeadingCount { get; init; }
}

/// <summary>
/// MCP tool that analyzes documentation coverage gaps.
/// Compares docs folder against test source_refs to identify uncovered areas.
/// </summary>
public sealed partial class AnalyzeCoverageGapsTool : IMcpTool
{
    private readonly string _basePath;
    private readonly IndexWriter _indexWriter;

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    public string Description => "Analyze documentation coverage gaps";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new
            {
                type = "string",
                description = "Suite to analyze coverage for (optional, analyzes all if omitted)"
            },
            docs_path = new
            {
                type = "string",
                description = "Documentation directory (optional, defaults to 'docs/')"
            }
        }
    };

    public AnalyzeCoverageGapsTool(string basePath, IndexWriter? indexWriter = null)
    {
        _basePath = basePath;
        _indexWriter = indexWriter ?? new IndexWriter();
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var suiteName = parameters?.TryGetProperty("suite", out var suiteEl) == true
            ? suiteEl.GetString()
            : null;

        var docsPath = parameters?.TryGetProperty("docs_path", out var docsEl) == true
            ? docsEl.GetString() ?? "docs"
            : "docs";

        var testsDir = Path.Combine(_basePath, "tests");
        var docsDir = Path.Combine(_basePath, docsPath);

        if (!Directory.Exists(testsDir))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TESTS_DIR_NOT_FOUND",
                "No tests/ directory found in repository root"));
        }

        if (!Directory.Exists(docsDir))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "DOCS_DIR_NOT_FOUND",
                $"No {docsPath}/ directory found in repository root"));
        }

        // Get suites to analyze
        var suites = GetSuitesToAnalyze(testsDir, suiteName);
        if (suites.Count == 0 && !string.IsNullOrEmpty(suiteName))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "SUITE_NOT_FOUND",
                $"Suite '{suiteName}' not found in tests/ directory"));
        }

        // Collect all source_refs from test indexes
        var coveredDocs = await CollectCoveredDocsAsync(suites);

        // Scan all documentation files
        var allDocs = await ScanDocumentationAsync(docsDir);

        // Calculate gaps
        var gaps = new List<DocCoverageGap>();
        var docsCovered = 0;

        foreach (var doc in allDocs)
        {
            var normalizedPath = NormalizePath(doc.DocumentPath);
            if (coveredDocs.Contains(normalizedPath))
            {
                docsCovered++;
            }
            else
            {
                gaps.Add(doc);
            }
        }

        var coveragePercent = allDocs.Count > 0
            ? (int)Math.Round((double)docsCovered / allDocs.Count * 100)
            : 100;

        var result = new
        {
            docs_scanned = allDocs.Count,
            docs_covered = docsCovered,
            coverage_percent = coveragePercent,
            gaps = gaps.Select(g => new
            {
                document_path = g.DocumentPath,
                document_title = g.DocumentTitle,
                severity = g.Severity.ToString().ToLowerInvariant(),
                size_kb = g.SizeKb,
                heading_count = g.HeadingCount
            })
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(result));
    }

    private List<string> GetSuitesToAnalyze(string testsDir, string? suiteName)
    {
        if (!string.IsNullOrEmpty(suiteName))
        {
            var suitePath = Path.Combine(testsDir, suiteName);
            return Directory.Exists(suitePath) ? [suitePath] : [];
        }

        return Directory.GetDirectories(testsDir).ToList();
    }

    private async Task<HashSet<string>> CollectCoveredDocsAsync(List<string> suiteDirs)
    {
        var coveredDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var suiteDir in suiteDirs)
        {
            var indexPath = IndexWriter.GetIndexPath(suiteDir);
            var index = await _indexWriter.ReadAsync(indexPath);

            if (index is null) continue;

            foreach (var test in index.Tests)
            {
                foreach (var sourceRef in test.SourceRefs)
                {
                    coveredDocs.Add(NormalizePath(sourceRef));
                }
            }
        }

        return coveredDocs;
    }

    private async Task<List<DocCoverageGap>> ScanDocumentationAsync(string docsDir)
    {
        var docs = new List<DocCoverageGap>();
        var docFiles = Directory.GetFiles(docsDir, "*.md", SearchOption.AllDirectories);

        foreach (var filePath in docFiles)
        {
            var content = await File.ReadAllTextAsync(filePath);
            var relativePath = Path.GetRelativePath(_basePath, filePath);
            var sizeKb = (int)Math.Ceiling(new FileInfo(filePath).Length / 1024.0);
            var headingCount = HeadingRegex().Matches(content).Count;
            var title = ExtractTitle(content) ?? Path.GetFileNameWithoutExtension(filePath);

            var severity = CalculateSeverity(sizeKb, headingCount);

            docs.Add(new DocCoverageGap
            {
                DocumentPath = NormalizePath(relativePath),
                DocumentTitle = title,
                Severity = severity,
                SizeKb = sizeKb,
                HeadingCount = headingCount
            });
        }

        return docs;
    }

    private static string? ExtractTitle(string content)
    {
        var match = TitleRegex().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static DocGapSeverity CalculateSeverity(int sizeKb, int headingCount)
    {
        if (sizeKb > 10 || headingCount > 5)
            return DocGapSeverity.High;

        if (sizeKb > 5 || headingCount > 2)
            return DocGapSeverity.Medium;

        return DocGapSeverity.Low;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
