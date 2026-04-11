using Spectra.CLI.Agent.Copilot;

namespace Spectra.CLI.Tests.Agent.Copilot;

/// <summary>
/// v1.46.0: the <c>{{testimize_strategy}}</c> placeholder in
/// <c>test-generation.md</c> is resolved from
/// <see cref="CopilotGenerationAgent.ResolveTestimizeStrategyToolName"/>.
/// </summary>
public class TestimizeStrategyResolverTests
{
    [Theory]
    [InlineData("HybridArtificialBeeColony")]
    [InlineData("hybridartificialbeecolony")] // different case
    [InlineData("")]
    [InlineData(null)]
    [InlineData("something-weird")]
    public void ResolveTestimizeStrategyToolName_HybridOrUnknown_ReturnsHybridTool(string? strategy)
    {
        var toolName = CopilotGenerationAgent.ResolveTestimizeStrategyToolName(strategy);
        Assert.Equal("testimize/generate_hybrid_test_cases", toolName);
    }

    [Theory]
    [InlineData("Pairwise")]
    [InlineData("PairwiseOptimized")]
    public void ResolveTestimizeStrategyToolName_Pairwise_ReturnsPairwiseTool(string strategy)
    {
        var toolName = CopilotGenerationAgent.ResolveTestimizeStrategyToolName(strategy);
        Assert.Equal("testimize/generate_pairwise_test_cases", toolName);
    }

    [Fact]
    public void ResolveTestimizeStrategyToolName_WhitespaceWrappedPairwise_ReturnsPairwiseTool()
    {
        var toolName = CopilotGenerationAgent.ResolveTestimizeStrategyToolName("  Pairwise  ");
        Assert.Equal("testimize/generate_pairwise_test_cases", toolName);
    }
}
