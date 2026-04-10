using Spectra.CLI.Agent.Testimize;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Testimize;

/// <summary>
/// Spec 038: GenerateTestData and AnalyzeFieldSpec AIFunction tools.
/// </summary>
public class TestimizeToolsTests
{
    [Fact]
    public async Task CreateGenerateTestDataTool_ProducesAIFunctionWithExpectedDescription()
    {
        await using var client = new TestimizeMcpClient();
        var tool = TestimizeTools.CreateGenerateTestDataTool(client, new TestimizeConfig());

        Assert.NotNull(tool);
        // Description must mention the techniques the tool implements (FR-018)
        Assert.Contains("boundary values", tool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("equivalence classes", tool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pairwise", tool.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateAnalyzeFieldSpecTool_ProducesAIFunction()
    {
        var tool = TestimizeTools.CreateAnalyzeFieldSpecTool();

        Assert.NotNull(tool);
        Assert.Equal("AnalyzeFieldSpec", tool.Name);
    }
}
