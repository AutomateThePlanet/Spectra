using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 037: test-update.md must include a Technique Completeness Check
/// section so the AI flags tests as OUTDATED when documentation introduces
/// new ranges, rules, states, or boundary changes.
/// </summary>
public class TestUpdateTemplateTests
{
    private static string Body() =>
        BuiltInTemplates.GetRawContent("test-update")
        ?? throw new Xunit.Sdk.XunitException("test-update template not found");

    [Fact]
    public void Template_ContainsTechniqueCompletenessCheckSection()
    {
        Assert.Contains("Technique Completeness Check", Body());
    }

    [Fact]
    public void Template_FlagsNewNumericRangeAsBvaOutdated()
    {
        var body = Body();
        Assert.Contains("BVA", body);
        Assert.Contains("numeric range", body);
        Assert.Contains("OUTDATED", body);
    }

    [Fact]
    public void Template_FlagsNewBusinessRulesAsDecisionTableOutdated()
    {
        var body = Body();
        Assert.Contains("Decision Table", body);
    }

    [Fact]
    public void Template_FlagsNewWorkflowStatesAsStateTransitionOutdated()
    {
        var body = Body();
        Assert.Contains("State Transition", body);
        Assert.Contains("workflow states", body);
    }

    [Fact]
    public void Template_FlagsBoundaryValueChanges()
    {
        var body = Body();
        Assert.Contains("boundary value", body);
    }
}
