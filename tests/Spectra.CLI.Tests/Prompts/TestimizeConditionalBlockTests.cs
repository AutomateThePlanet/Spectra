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
        // v1.46.0: AnalyzeFieldSpec is still referenced (local tool), but the
        // old GenerateTestData wrapper was deleted in favor of two real MCP
        // tool names: testimize/generate_hybrid_test_cases and
        // testimize/generate_pairwise_test_cases.
        Assert.Contains("AnalyzeFieldSpec", body);
        Assert.Contains("testimize/generate_hybrid_test_cases", body);
        Assert.Contains("testimize/generate_pairwise_test_cases", body);
    }

    [Fact]
    public void TestGeneration_Template_ContainsTestimizeConditionalBlock()
    {
        var body = BuiltInTemplates.GetRawContent("test-generation")!;
        Assert.Contains("{{#if testimize_enabled}}", body);
        // v1.46.0: heading changed from "USING TESTIMIZE-GENERATED VALUES"
        // to "USING TESTIMIZE FOR OPTIMIZED TEST DATA" to reflect that the
        // AI now picks between two real MCP tools instead of calling a
        // wrapper.
        Assert.Contains("USING TESTIMIZE", body);
        Assert.Contains("testimize/generate_hybrid_test_cases", body);
        Assert.Contains("testimize/generate_pairwise_test_cases", body);
        Assert.Contains("{{testimize_strategy}}", body);
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
