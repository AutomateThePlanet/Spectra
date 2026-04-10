using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 037: built-in templates ship with ISTQB technique guidance, and the
/// init/upgrade flow preserves user-edited copies (per spec 022 hash-tracking).
/// </summary>
public class PromptTemplateMigrationTests
{
    [Fact]
    public void AllFiveTemplates_ContainExpectedTechniqueMarkers()
    {
        // behavior-analysis: full ISTQB technique sections
        var ba = BuiltInTemplates.GetRawContent("behavior-analysis");
        Assert.NotNull(ba);
        Assert.Contains("Equivalence Partitioning", ba);
        Assert.Contains("Boundary Value Analysis", ba);

        // test-generation: technique step-writing rules
        var tg = BuiltInTemplates.GetRawContent("test-generation");
        Assert.NotNull(tg);
        Assert.Contains("TEST DESIGN TECHNIQUE RULES", tg);

        // test-update: technique completeness check
        var tu = BuiltInTemplates.GetRawContent("test-update");
        Assert.NotNull(tu);
        Assert.Contains("Technique Completeness Check", tu);

        // critic-verification: technique verification
        var cv = BuiltInTemplates.GetRawContent("critic-verification");
        Assert.NotNull(cv);
        Assert.Contains("Technique Verification", cv);

        // criteria-extraction: technique hints
        var ce = BuiltInTemplates.GetRawContent("criteria-extraction");
        Assert.NotNull(ce);
        Assert.Contains("Technique Hints", ce);
    }

    [Fact]
    public void BuiltInTemplates_AllFiveExposed()
    {
        Assert.Equal(5, BuiltInTemplates.AllTemplateIds.Count);
        Assert.Contains("behavior-analysis", BuiltInTemplates.AllTemplateIds);
        Assert.Contains("test-generation", BuiltInTemplates.AllTemplateIds);
        Assert.Contains("test-update", BuiltInTemplates.AllTemplateIds);
        Assert.Contains("critic-verification", BuiltInTemplates.AllTemplateIds);
        Assert.Contains("criteria-extraction", BuiltInTemplates.AllTemplateIds);
    }
}
