using System.Text.Json;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.Core.Parsing;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.Data;

/// <summary>
/// MCP tool that rebuilds _index.json files from test files on disk.
/// Returns rebuild statistics including files added/removed.
/// </summary>
public sealed class RebuildIndexesTool : IMcpTool
{
    private readonly string _basePath;
    private readonly IndexGenerator _generator;
    private readonly IndexWriter _writer;
    private readonly TestCaseParser _parser;

    public string Description => "Rebuild _index.json files from test files";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new
            {
                type = "string",
                description = "Suite name to rebuild (optional, rebuilds all if omitted)"
            }
        }
    };

    public RebuildIndexesTool(
        string basePath,
        IndexGenerator? generator = null,
        IndexWriter? writer = null,
        TestCaseParser? parser = null)
    {
        _basePath = basePath;
        _generator = generator ?? new IndexGenerator();
        _writer = writer ?? new IndexWriter();
        _parser = parser ?? new TestCaseParser();
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var suiteName = parameters?.TryGetProperty("suite", out var suiteEl) == true
            ? suiteEl.GetString()
            : null;

        var testsDir = Path.Combine(_basePath, "test-cases");
        if (!Directory.Exists(testsDir))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TESTS_DIR_NOT_FOUND",
                "No test-cases/ directory found in repository root"));
        }

        var suites = GetSuitesToRebuild(testsDir, suiteName);
        if (suites.Count == 0 && !string.IsNullOrEmpty(suiteName))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "SUITE_NOT_FOUND",
                $"Suite '{suiteName}' not found in test-cases/ directory"));
        }

        var totalSuites = 0;
        var totalTests = 0;
        var totalAdded = 0;
        var totalRemoved = 0;
        var indexPaths = new List<string>();

        foreach (var suiteDir in suites)
        {
            try
            {
                var (testsIndexed, added, removed, indexPath) = await RebuildSuiteIndexAsync(suiteDir);
                totalSuites++;
                totalTests += testsIndexed;
                totalAdded += added;
                totalRemoved += removed;
                indexPaths.Add(Path.GetRelativePath(_basePath, indexPath));
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "INDEX_WRITE_ERROR",
                    $"Failed to write index for {Path.GetFileName(suiteDir)}: {ex.Message}"));
            }
        }

        var result = new
        {
            suites_processed = totalSuites,
            tests_indexed = totalTests,
            files_added = totalAdded,
            files_removed = totalRemoved,
            index_paths = indexPaths
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(result));
    }

    private List<string> GetSuitesToRebuild(string testsDir, string? suiteName)
    {
        if (!string.IsNullOrEmpty(suiteName))
        {
            var suitePath = Path.Combine(testsDir, suiteName);
            return Directory.Exists(suitePath) ? [suitePath] : [];
        }

        return Directory.GetDirectories(testsDir).ToList();
    }

    private async Task<(int testsIndexed, int added, int removed, string indexPath)>
        RebuildSuiteIndexAsync(string suiteDir)
    {
        var suiteName = Path.GetFileName(suiteDir);
        var indexPath = IndexWriter.GetIndexPath(suiteDir);

        // Load existing index to track changes
        var existingIndex = await _writer.ReadAsync(indexPath);
        var existingIds = existingIndex?.Tests.Select(t => t.Id).ToHashSet() ?? [];

        // Parse all test files
        var testCases = new List<TestCase>();
        var testFiles = Directory.GetFiles(suiteDir, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();

        foreach (var filePath in testFiles)
        {
            var content = await File.ReadAllTextAsync(filePath);
            var relativePath = Path.GetRelativePath(suiteDir, filePath);
            var parseResult = _parser.Parse(content, relativePath);

            if (parseResult.IsSuccess)
            {
                testCases.Add(parseResult.Value);
            }
        }

        // Generate new index
        var newIndex = _generator.Generate(suiteName, testCases);
        var newIds = newIndex.Tests.Select(t => t.Id).ToHashSet();

        // Calculate delta
        var added = newIds.Except(existingIds).Count();
        var removed = existingIds.Except(newIds).Count();

        // Write index
        await _writer.WriteAsync(indexPath, newIndex);

        return (newIndex.Tests.Count, added, removed, indexPath);
    }
}
