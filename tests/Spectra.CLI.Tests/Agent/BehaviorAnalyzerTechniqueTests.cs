using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Agent.Copilot;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 037: IdentifiedBehavior must round-trip the new `technique` field and
/// remain backward-compatible with legacy AI responses that omit it.
/// </summary>
public class BehaviorAnalyzerTechniqueTests
{
    [Fact]
    public void ParseAnalysisResponse_WithTechniqueField_PopulatesTechnique()
    {
        var json = """
            {
              "behaviors": [
                {"category": "boundary", "title": "Field rejects 21 chars (max 20)", "source": "docs/form.md", "technique": "BVA"},
                {"category": "negative", "title": "Member discount with $50 order", "source": "docs/checkout.md", "technique": "DT"}
              ]
            }
            """;

        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("BVA", result[0].Technique);
        Assert.Equal("DT", result[1].Technique);
    }

    [Fact]
    public void ParseAnalysisResponse_WithoutTechniqueField_DefaultsToEmptyString()
    {
        // Legacy AI response — no technique field at all
        var json = """
            {
              "behaviors": [
                {"category": "happy_path", "title": "Successful login", "source": "docs/auth.md"}
              ]
            }
            """;

        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("", result[0].Technique);
    }

    [Fact]
    public void IdentifiedBehavior_TechniqueDefaultsToEmptyString()
    {
        var behavior = new IdentifiedBehavior
        {
            Category = "happy_path",
            Title = "Test",
            Source = "docs/x.md"
        };

        Assert.Equal("", behavior.Technique);
    }
}
