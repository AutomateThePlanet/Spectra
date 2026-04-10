using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 038: behavior-analysis.md and test-generation.md must include
/// {{#if testimize_enabled}} blocks that render only when the placeholder
/// value is non-empty.
/// </summary>
public class TestimizeConditionalBlockTests
{
    [Fact]
    public void BehaviorAnalysis_Template_ContainsTestimizeConditionalBlock()
    {
        var body = BuiltInTemplates.GetRawContent("behavior-analysis")!;
        Assert.Contains("{{#if testimize_enabled}}", body);
        Assert.Contains("ALGORITHMIC TEST DATA", body);
        Assert.Contains("AnalyzeFieldSpec", body);
        Assert.Contains("GenerateTestData", body);
    }

    [Fact]
    public void TestGeneration_Template_ContainsTestimizeConditionalBlock()
    {
        var body = BuiltInTemplates.GetRawContent("test-generation")!;
        Assert.Contains("{{#if testimize_enabled}}", body);
        Assert.Contains("USING TESTIMIZE-GENERATED VALUES", body);
        Assert.Contains("EXACT values from Testimize", body);
    }

    [Fact]
    public void Resolve_TestimizeEnabledTrue_RendersBlock()
    {
        var template = BuiltInTemplates.GetTemplate("behavior-analysis")!;
        var values = new Dictionary<string, string>
        {
            ["testimize_enabled"] = "true",
            ["document_text"] = "doc",
            ["focus_areas"] = "",
            ["acceptance_criteria"] = ""
        };
        var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["categories"] = []
        };

        var rendered = PromptTemplateLoader.Resolve(template, values, listValues);

        Assert.Contains("ALGORITHMIC TEST DATA", rendered);
    }

    [Fact]
    public void Resolve_TestimizeEnabledEmpty_HidesBlock()
    {
        var template = BuiltInTemplates.GetTemplate("behavior-analysis")!;
        var values = new Dictionary<string, string>
        {
            ["testimize_enabled"] = "",
            ["document_text"] = "doc",
            ["focus_areas"] = "",
            ["acceptance_criteria"] = ""
        };
        var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["categories"] = []
        };

        var rendered = PromptTemplateLoader.Resolve(template, values, listValues);

        Assert.DoesNotContain("ALGORITHMIC TEST DATA", rendered);
    }
}
