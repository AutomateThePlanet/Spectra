using System.Text.Json;
using Spectra.Core.Models;
using Spectra.MCP.Server;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools;

/// <summary>
/// Spec 051 — find_test_cases rejects a nested 'filters' object with an actionable
/// error pointing at the canonical top-level shape.
/// </summary>
public class FindTestCasesActionableErrorTests
{
    private static FindTestCasesTool MakeTool() =>
        new(
            suiteListLoader: () => ["checkout"],
            indexLoader: _ =>
            [
                new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "One", Priority = "high", Tags = ["smoke"] }
            ]);

    [Fact]
    public async Task FindTestCases_NestedFiltersObject_ReturnsActionableError()
    {
        var tool = MakeTool();
        var parameters = JsonDocument.Parse("""{"suites": ["checkout"], "filters": {"priority": "high"}}""").RootElement;

        var ex = await Assert.ThrowsAsync<McpInvalidParamsException>(() => tool.ExecuteAsync(parameters));

        Assert.Contains("filters", ex.Message);
        Assert.Contains("top-level", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("priorities", ex.Message);
    }

    [Fact]
    public async Task FindTestCases_TopLevelPriorities_Succeeds()
    {
        // Canonical shape on find_test_cases is unaffected.
        var tool = MakeTool();
        var parameters = JsonDocument.Parse("""{"suites": ["checkout"], "priorities": ["high"]}""").RootElement;

        var result = await tool.ExecuteAsync(parameters);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out _));
    }
}
