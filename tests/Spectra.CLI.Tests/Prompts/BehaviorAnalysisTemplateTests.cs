using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 037: behavior-analysis.md must teach the AI to apply ISTQB test design
/// techniques systematically and request a `technique` field per behavior.
/// </summary>
public class BehaviorAnalysisTemplateTests
{
    private static string Body() =>
        BuiltInTemplates.GetRawContent("behavior-analysis")
        ?? throw new Xunit.Sdk.XunitException("behavior-analysis template not found");

    [Fact]
    public void Template_ContainsAllSixTechniqueInstructions()
    {
        var body = Body();

        Assert.Contains("Equivalence Partitioning", body);
        Assert.Contains("Boundary Value Analysis", body);
        Assert.Contains("Decision Table", body);
        Assert.Contains("State Transition", body);
        Assert.Contains("Error Guessing", body);
        Assert.Contains("Use Case", body);
    }

    [Fact]
    public void Template_RequestsTechniqueFieldInJsonOutput()
    {
        var body = Body();

        Assert.Contains("technique", body);
        // The example JSON snippet must include the technique key
        Assert.Contains("\"technique\":", body);
    }

    [Fact]
    public void Template_Contains40PercentDistributionCap()
    {
        var body = Body();

        Assert.Contains("40%", body);
        // The cap must reference categories generically, not hardcode happy_path
        Assert.Contains("any single category", body);
    }

    [Fact]
    public void Template_RequiresAtLeast4BvaBehaviorsPerRange()
    {
        var body = Body();

        // The "at least 4" requirement must be present for numeric ranges
        Assert.Contains("at least 4 BVA", body);
    }

    [Fact]
    public void Template_RequiresInvalidStateTransitionPerWorkflow()
    {
        var body = Body();

        Assert.Contains("invalid state transition", body);
    }

    [Fact]
    public void Template_DoesNotHardcodeHappyPathInDistributionRule()
    {
        var body = Body();

        // The 40% cap must not single out happy_path; it must apply to "any single category"
        var distributionLine = body.Split('\n')
            .FirstOrDefault(l => l.Contains("40%") && l.Contains("category", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(distributionLine);
        Assert.DoesNotContain("happy_path", distributionLine, StringComparison.OrdinalIgnoreCase);
    }
}
