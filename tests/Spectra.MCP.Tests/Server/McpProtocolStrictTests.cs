using System.Text.Json;
using Spectra.Core.Models;
using Spectra.MCP.Server;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Server;

/// <summary>
/// Spec 051 — strict unmapped-member handling at the MCP parameter boundary.
/// The strict path lives in the single shared <see cref="McpProtocol.DeserializeParams{T}"/>
/// used by every tool; these tests exercise it through a representative tool
/// (find_test_cases, the Data family) to complement the RunManagement-family
/// coverage in StartExecutionRunTests.
/// </summary>
public class McpProtocolStrictTests
{
    private static FindTestCasesTool MakeTool() =>
        new(
            suiteListLoader: () => ["checkout"],
            indexLoader: _ =>
            [
                new TestIndexEntry { Id = "TC-001", File = "tc-001.md", Title = "One", Priority = "high", Tags = ["smoke"] }
            ]);

    [Fact]
    public async Task DeserializerDisallow_UnknownField_ThrowsStructuredError_NotSilentDrop()
    {
        var tool = MakeTool();
        var parameters = JsonDocument.Parse("""{"suites": ["checkout"], "bogus_xyz": 1}""").RootElement;

        var ex = await Assert.ThrowsAsync<McpInvalidParamsException>(() => tool.ExecuteAsync(parameters));

        // The offending property is named (regex extraction works) ...
        Assert.Contains("bogus_xyz", ex.Message);
        Assert.Contains("find_test_cases", ex.Message);
        // ... and an unknown non-filter field gets the generic guidance (no false suggestion).
        Assert.Contains("Check the tool schema", ex.Message);
    }

    [Fact]
    public async Task DeserializerDisallow_ValidParams_StillDeserialize()
    {
        // Strictness must not reject well-formed requests.
        var tool = MakeTool();
        var parameters = JsonDocument.Parse("""{"suites": ["checkout"], "priorities": ["high"], "max_results": 10}""").RootElement;

        var result = await tool.ExecuteAsync(parameters);

        Assert.Contains("\"data\"", result);
    }
}
