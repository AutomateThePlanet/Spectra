using System.Text.Json;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools.Data;

/// <summary>
/// End-to-end tests that validate the quickstart.md scenarios work correctly.
/// T075: Run quickstart.md validation scenarios end-to-end
/// </summary>
public class QuickstartScenariosTests : IDisposable
{
    private readonly string _testDir;

    public QuickstartScenariosTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-quickstart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        SetupTestScenario();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void SetupTestScenario()
    {
        // Create tests/checkout suite
        var checkoutDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(checkoutDir);

        // Valid test file
        File.WriteAllText(Path.Combine(checkoutDir, "tc-001.md"), """
            ---
            id: TC-001
            priority: high
            tags: [checkout, payment]
            source_refs: [docs/features/checkout.md]
            ---

            # Add item to cart

            ## Steps

            1. Navigate to product page
            2. Click "Add to Cart" button
            3. Verify cart icon shows quantity

            ## Expected Result

            Item appears in cart with correct quantity.
            """);

        // Test file with missing ID (should cause validation error)
        File.WriteAllText(Path.Combine(checkoutDir, "missing-id.md"), """
            ---
            priority: medium
            tags: [checkout]
            ---

            # Missing ID Test

            ## Steps

            1. Step one

            ## Expected Result

            This test has no ID and should fail validation.
            """);

        // Create index
        File.WriteAllText(Path.Combine(checkoutDir, "_index.json"), """
            {
                "suite": "checkout",
                "generated_at": "2026-01-01T00:00:00Z",
                "tests": [
                    {
                        "id": "TC-001",
                        "file": "tc-001.md",
                        "title": "Add item to cart",
                        "priority": "high",
                        "tags": ["checkout", "payment"],
                        "source_refs": ["docs/features/checkout.md"]
                    }
                ]
            }
            """);

        // Create docs
        var docsDir = Path.Combine(_testDir, "docs", "features");
        Directory.CreateDirectory(docsDir);

        // Covered document
        File.WriteAllText(Path.Combine(docsDir, "checkout.md"), """
            # Checkout Feature

            The checkout feature allows users to complete their purchase.

            ## Cart Management

            Add items, update quantities, remove items.

            ## Payment Processing

            Support for credit cards, PayPal, and gift cards.
            """);

        // Uncovered document (should appear in coverage gaps)
        File.WriteAllText(Path.Combine(docsDir, "refunds.md"), """
            # Refund Processing

            ## Full Refunds

            Process complete refunds for returned items.

            ## Partial Refunds

            Handle partial refund scenarios.

            ## Refund Policies

            Standard policies for refund requests.
            """);
    }

    [Fact]
    public async Task Scenario_ValidateTests_DetectsMissingId()
    {
        // Arrange - Per quickstart.md: "Create test file with missing ID, call validate_tests"
        var tool = new ValidateTestsTool(_testDir);

        // Act
        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"checkout\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        // Assert - Per quickstart.md: "verify error code MISSING_ID returned"
        var data = response.GetProperty("data");
        Assert.False(data.GetProperty("is_valid").GetBoolean());

        var errors = data.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);

        // Find the MISSING_ID error
        var hasIdError = false;
        foreach (var error in errors.EnumerateArray())
        {
            var code = error.GetProperty("code").GetString();
            if (code == "MISSING_ID")
            {
                hasIdError = true;
                Assert.Contains("missing-id.md", error.GetProperty("file_path").GetString());
            }
        }
        Assert.True(hasIdError, "Should have MISSING_ID error");
    }

    [Fact]
    public async Task Scenario_RebuildIndexes_UpdatesIndex()
    {
        // Arrange - Per quickstart.md: "Add test file manually, call rebuild_indexes"
        var tool = new RebuildIndexesTool(_testDir);

        // Act
        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"checkout\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        // Assert - Per quickstart.md: "verify new entry in _index.json"
        var data = response.GetProperty("data");
        Assert.Equal(1, data.GetProperty("suites_processed").GetInt32());
        Assert.True(data.GetProperty("tests_indexed").GetInt32() >= 1);
    }

    [Fact]
    public async Task Scenario_AnalyzeCoverageGaps_FindsUncoveredDocs()
    {
        // Arrange - Per quickstart.md: "Create doc file with no test coverage, call analyze_coverage_gaps"
        var tool = new AnalyzeCoverageGapsTool(_testDir);

        // Act
        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        // Assert - Per quickstart.md: "verify doc appears in gaps list"
        var data = response.GetProperty("data");
        Assert.Equal(2, data.GetProperty("docs_scanned").GetInt32());
        Assert.Equal(1, data.GetProperty("docs_covered").GetInt32()); // checkout.md is covered
        Assert.Equal(50, data.GetProperty("coverage_percent").GetInt32()); // 1/2 = 50%

        var gaps = data.GetProperty("gaps");
        Assert.Equal(1, gaps.GetArrayLength());

        var gap = gaps[0];
        Assert.Contains("refunds", gap.GetProperty("document_path").GetString());
        Assert.Equal("Refund Processing", gap.GetProperty("document_title").GetString());
    }

    [Fact]
    public async Task Scenario_AllToolsReturnCorrectStructure()
    {
        // Verify output structure matches quickstart.md examples
        var validateTool = new ValidateTestsTool(_testDir);
        var rebuildTool = new RebuildIndexesTool(_testDir);
        var coverageTool = new AnalyzeCoverageGapsTool(_testDir);

        // Validate Tests - check structure from quickstart example
        var validateResult = await validateTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var validateResponse = JsonDocument.Parse(validateResult).RootElement;
        var validateData = validateResponse.GetProperty("data");
        Assert.True(validateData.TryGetProperty("is_valid", out _));
        Assert.True(validateData.TryGetProperty("total_files", out _));
        Assert.True(validateData.TryGetProperty("valid_files", out _));
        Assert.True(validateData.TryGetProperty("errors", out _));

        // Rebuild Indexes - check structure from quickstart example
        var rebuildResult = await rebuildTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var rebuildResponse = JsonDocument.Parse(rebuildResult).RootElement;
        var rebuildData = rebuildResponse.GetProperty("data");
        Assert.True(rebuildData.TryGetProperty("suites_processed", out _));
        Assert.True(rebuildData.TryGetProperty("tests_indexed", out _));

        // Coverage Gaps - check structure from quickstart example
        var coverageResult = await coverageTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var coverageResponse = JsonDocument.Parse(coverageResult).RootElement;
        var coverageData = coverageResponse.GetProperty("data");
        Assert.True(coverageData.TryGetProperty("docs_scanned", out _));
        Assert.True(coverageData.TryGetProperty("docs_covered", out _));
        Assert.True(coverageData.TryGetProperty("coverage_percent", out _));
        Assert.True(coverageData.TryGetProperty("gaps", out _));
    }

    [Fact]
    public async Task Scenario_ToolErrors_MatchQuickstartErrorCodes()
    {
        // Verify error codes from quickstart.md error reference
        var validateTool = new ValidateTestsTool(_testDir);
        var coverageTool = new AnalyzeCoverageGapsTool(_testDir);

        // SUITE_NOT_FOUND
        var suiteResult = await validateTool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"nonexistent\"}").RootElement);
        var suiteResponse = JsonDocument.Parse(suiteResult).RootElement;
        Assert.Equal("SUITE_NOT_FOUND", suiteResponse.GetProperty("error").GetProperty("code").GetString());

        // TESTS_DIR_NOT_FOUND (use empty temp dir)
        var emptyDir = Path.Combine(Path.GetTempPath(), $"spectra-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);
        Directory.CreateDirectory(Path.Combine(emptyDir, "docs"));
        try
        {
            var emptyTool = new ValidateTestsTool(emptyDir);
            var emptyResult = await emptyTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
            var emptyResponse = JsonDocument.Parse(emptyResult).RootElement;
            Assert.Equal("TESTS_DIR_NOT_FOUND", emptyResponse.GetProperty("error").GetProperty("code").GetString());
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }

        // DOCS_DIR_NOT_FOUND
        var noDocsDir = Path.Combine(Path.GetTempPath(), $"spectra-nodocs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(noDocsDir, "tests", "suite"));
        File.WriteAllText(Path.Combine(noDocsDir, "tests", "suite", "_index.json"), "{\"suite\":\"suite\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[]}");
        try
        {
            var noDocsTool = new AnalyzeCoverageGapsTool(noDocsDir);
            var noDocsResult = await noDocsTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
            var noDocsResponse = JsonDocument.Parse(noDocsResult).RootElement;
            Assert.Equal("DOCS_DIR_NOT_FOUND", noDocsResponse.GetProperty("error").GetProperty("code").GetString());
        }
        finally
        {
            Directory.Delete(noDocsDir, true);
        }
    }
}
