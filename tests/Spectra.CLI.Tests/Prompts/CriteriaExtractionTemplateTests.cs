using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 037: criteria-extraction.md must instruct the AI to emit an optional
/// `technique_hint` field per criterion based on whether the criterion text
/// describes a numeric range, conditional logic, state change, or input class.
/// </summary>
public class CriteriaExtractionTemplateTests
{
    private static string Body() =>
        BuiltInTemplates.GetRawContent("criteria-extraction")
        ?? throw new Xunit.Sdk.XunitException("criteria-extraction template not found");

    [Fact]
    public void Template_ContainsTechniqueHintsSection()
    {
        Assert.Contains("Technique Hints", Body());
    }

    [Fact]
    public void Template_MapsNumericRangeToBva()
    {
        var body = Body();
        Assert.Contains("numeric range", body);
        Assert.Contains("BVA", body);
    }

    [Fact]
    public void Template_MapsMultipleConditionsToDt()
    {
        var body = Body();
        Assert.Contains("multiple conditions", body);
        Assert.Contains("DT", body);
    }

    [Fact]
    public void Template_MapsWorkflowStateToSt()
    {
        var body = Body();
        Assert.Contains("workflow", body);
        Assert.Contains("ST", body);
    }

    [Fact]
    public void Template_MapsInputCategoriesToEp()
    {
        var body = Body();
        Assert.Contains("valid/invalid input categories", body);
        Assert.Contains("EP", body);
    }

    [Fact]
    public void Template_TechniqueHintFieldDocumentedInJsonExample()
    {
        var body = Body();
        Assert.Contains("technique_hint", body);
    }
}
