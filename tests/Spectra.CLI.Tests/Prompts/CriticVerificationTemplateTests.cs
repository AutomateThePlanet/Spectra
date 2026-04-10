using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 037: critic-verification.md must include technique-aware verification
/// rules so the critic checks BVA boundary values, EP equivalence classes,
/// ST transition paths, and DT condition combinations against documentation.
/// </summary>
public class CriticVerificationTemplateTests
{
    private static string Body() =>
        BuiltInTemplates.GetRawContent("critic-verification")
        ?? throw new Xunit.Sdk.XunitException("critic-verification template not found");

    [Fact]
    public void Template_ContainsTechniqueVerificationSection()
    {
        Assert.Contains("Technique Verification", Body());
    }

    [Fact]
    public void Template_BvaRule_FlagsBoundaryMismatchAsPartial()
    {
        var body = Body();
        Assert.Contains("BVA tests", body);
        Assert.Contains("PARTIAL", body);
        Assert.Contains("documented", body);
    }

    [Fact]
    public void Template_EpRule_FlagsUndocumentedClassAsPartial()
    {
        var body = Body();
        Assert.Contains("EP tests", body);
        Assert.Contains("equivalence class", body);
    }

    [Fact]
    public void Template_StRule_FlagsUnsupportedTransitionAsPartial()
    {
        var body = Body();
        Assert.Contains("ST tests", body);
        Assert.Contains("transition", body);
    }

    [Fact]
    public void Template_DtRule_FlagsMissingConditionAsHallucinated()
    {
        var body = Body();
        Assert.Contains("DT tests", body);
        Assert.Contains("HALLUCINATED", body);
    }
}
