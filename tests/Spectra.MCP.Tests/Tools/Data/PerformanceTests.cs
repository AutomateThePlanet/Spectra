using System.Diagnostics;
using System.Text.Json;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools.Data;

/// <summary>
/// Performance tests for MCP data tools.
/// T076: Verify all MCP tools complete in &lt;5s for 500 test file repository
/// </summary>
public class PerformanceTests : IDisposable
{
    private const int TestFileCount = 500;
    private const int MaxDurationSeconds = 5;
    private readonly string _testDir;

    public PerformanceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        SetupLargeRepository();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void SetupLargeRepository()
    {
        // Create 5 suites with 100 tests each = 500 tests
        var suites = new[] { "auth", "checkout", "orders", "payments", "users" };

        foreach (var suite in suites)
        {
            var suiteDir = Path.Combine(_testDir, "test-cases", suite);
            Directory.CreateDirectory(suiteDir);

            var indexEntries = new List<string>();

            for (int i = 1; i <= 100; i++)
            {
                var testId = $"TC-{suite.ToUpper()}-{i:D3}";
                var fileName = $"tc-{i:D3}.md";

                // Create test file
                var testContent = $"""
                    ---
                    id: {testId}
                    priority: {(i % 3 == 0 ? "high" : i % 2 == 0 ? "medium" : "low")}
                    tags: [{suite}, test-{i}]
                    source_refs: [docs/{suite}/feature-{(i % 10) + 1}.md]
                    ---

                    # Test {i} for {suite}

                    ## Steps

                    1. Step one for test {i}
                    2. Step two for test {i}
                    3. Step three for test {i}

                    ## Expected Result

                    Expected result for test {i} in {suite} suite.
                    """;

                File.WriteAllText(Path.Combine(suiteDir, fileName), testContent);

                // Build index entry
                var priority = i % 3 == 0 ? "high" : i % 2 == 0 ? "medium" : "low";
                var featureNum = (i % 10) + 1;
                indexEntries.Add($@"{{
    ""id"": ""{testId}"",
    ""file"": ""{fileName}"",
    ""title"": ""Test {i} for {suite}"",
    ""priority"": ""{priority}"",
    ""tags"": [""{suite}"", ""test-{i}""],
    ""source_refs"": [""docs/{suite}/feature-{featureNum}.md""]
}}");
            }

            // Write index file
            var indexContent = $@"{{
    ""suite"": ""{suite}"",
    ""generated_at"": ""2026-01-01T00:00:00Z"",
    ""tests"": [
        {string.Join(",\n        ", indexEntries)}
    ]
}}";
            File.WriteAllText(Path.Combine(suiteDir, "_index.json"), indexContent);

            // Create docs for this suite
            var docsDir = Path.Combine(_testDir, "docs", suite);
            Directory.CreateDirectory(docsDir);
            for (int i = 1; i <= 10; i++)
            {
                var docContent = $"""
                    # Feature {i} for {suite}

                    ## Overview

                    Description of feature {i} in the {suite} module.

                    ## Details

                    Additional details for feature {i}.
                    """;
                File.WriteAllText(Path.Combine(docsDir, $"feature-{i}.md"), docContent);
            }
        }
    }

    [Fact]
    public async Task ValidateTests_Completes_Under5Seconds_With500Files()
    {
        // Arrange
        var tool = new ValidateTestsTool(_testDir);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        stopwatch.Stop();

        // Assert
        var response = JsonDocument.Parse(result).RootElement;
        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(TestFileCount, data.GetProperty("total_files").GetInt32());
        Assert.True(stopwatch.Elapsed.TotalSeconds < MaxDurationSeconds,
            $"ValidateTests took {stopwatch.Elapsed.TotalSeconds:F2}s, expected <{MaxDurationSeconds}s");
    }

    [Fact]
    public async Task RebuildIndexes_Completes_Under5Seconds_With500Files()
    {
        // Arrange
        var tool = new RebuildIndexesTool(_testDir);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        stopwatch.Stop();

        // Assert
        var response = JsonDocument.Parse(result).RootElement;
        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(5, data.GetProperty("suites_processed").GetInt32());
        Assert.Equal(TestFileCount, data.GetProperty("tests_indexed").GetInt32());
        Assert.True(stopwatch.Elapsed.TotalSeconds < MaxDurationSeconds,
            $"RebuildIndexes took {stopwatch.Elapsed.TotalSeconds:F2}s, expected <{MaxDurationSeconds}s");
    }

    [Fact]
    public async Task AnalyzeCoverageGaps_Completes_Under5Seconds_With500Files()
    {
        // Arrange
        var tool = new AnalyzeCoverageGapsTool(_testDir);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        stopwatch.Stop();

        // Assert
        var response = JsonDocument.Parse(result).RootElement;
        Assert.True(response.TryGetProperty("data", out var data));
        Assert.Equal(50, data.GetProperty("docs_scanned").GetInt32()); // 5 suites * 10 docs
        Assert.True(stopwatch.Elapsed.TotalSeconds < MaxDurationSeconds,
            $"AnalyzeCoverageGaps took {stopwatch.Elapsed.TotalSeconds:F2}s, expected <{MaxDurationSeconds}s");
    }

    [Fact]
    public async Task AllTools_ReportPerformanceMetrics()
    {
        // Run all tools and output timing for analysis
        var validateTool = new ValidateTestsTool(_testDir);
        var rebuildTool = new RebuildIndexesTool(_testDir);
        var coverageTool = new AnalyzeCoverageGapsTool(_testDir);

        var sw1 = Stopwatch.StartNew();
        await validateTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        await rebuildTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        sw2.Stop();

        var sw3 = Stopwatch.StartNew();
        await coverageTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        sw3.Stop();

        // Log performance metrics
        var totalMs = sw1.ElapsedMilliseconds + sw2.ElapsedMilliseconds + sw3.ElapsedMilliseconds;

        // All tools should complete combined in under 15s (5s each)
        Assert.True(totalMs < 15000,
            $"All tools combined took {totalMs}ms, expected <15000ms\n" +
            $"  ValidateTests: {sw1.ElapsedMilliseconds}ms\n" +
            $"  RebuildIndexes: {sw2.ElapsedMilliseconds}ms\n" +
            $"  AnalyzeCoverageGaps: {sw3.ElapsedMilliseconds}ms");
    }
}
