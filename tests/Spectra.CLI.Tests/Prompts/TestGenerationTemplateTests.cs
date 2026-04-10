using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 037: test-generation.md must include ISTQB technique-aware step-writing
/// rules so AI generates tests with concrete boundary values, named equivalence
/// classes, explicit conditions, and state-transition narratives.
/// </summary>
public class TestGenerationTemplateTests
{
    private static string Body() =>
        BuiltInTemplates.GetRawContent("test-generation")
        ?? throw new Xunit.Sdk.XunitException("test-generation template not found");

    [Fact]
    public void Template_ContainsTestDesignTechniqueRulesSection()
    {
        Assert.Contains("TEST DESIGN TECHNIQUE RULES", Body());
    }

    [Fact]
    public void Template_ContainsBvaExactValueRule()
    {
        var body = Body();
        Assert.Contains("BVA-tagged behaviors", body);
        Assert.Contains("EXACT boundary values", body);
    }

    [Fact]
    public void Template_ContainsEpEquivalenceClassRule()
    {
        var body = Body();
        Assert.Contains("EP-tagged behaviors", body);
        Assert.Contains("equivalence class", body);
    }

    [Fact]
    public void Template_ContainsDtConditionsRule()
    {
        var body = Body();
        Assert.Contains("DT-tagged behaviors", body);
        Assert.Contains("condition values", body);
    }

    [Fact]
    public void Template_ContainsStStartingActionResultingRule()
    {
        var body = Body();
        Assert.Contains("ST-tagged behaviors", body);
        Assert.Contains("starting state", body);
        Assert.Contains("resulting state", body);
    }

    [Fact]
    public void Template_ContainsEgConcreteScenarioRule()
    {
        var body = Body();
        Assert.Contains("EG-tagged behaviors", body);
        Assert.Contains("specific error scenario", body);
    }
}
