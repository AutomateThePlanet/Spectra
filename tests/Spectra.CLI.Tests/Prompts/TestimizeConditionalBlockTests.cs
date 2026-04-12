using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// v1.48.3: behavior-analysis.md and test-generation.md conditional blocks
/// for the in-process Testimize flow. The behavior-analysis template gates
/// on {{#if testimize_enabled}} and asks the AI to emit a field_specs[]
/// array. The test-generation template gates on {{#if testimize_dataset}}
/// and embeds pre-computed values with Testimize attribution.
/// </summary>
public class TestimizeConditionalBlockTests
{
    [Fact]
    public void BehaviorAnalysis_Template_AsksForFieldSpecs()
    {
        var body = BuiltInTemplates.GetRawContent("behavior-analysis")!;
        Assert.Contains("{{#if testimize_enabled}}", body);
        Assert.Contains("STRUCTURED FIELD SPECIFICATIONS", body);
        Assert.Contains("field_specs", body);
        // The old MCP tool references must not survive the refactor.
        Assert.DoesNotContain("testimize/generate_hybrid_test_cases", body);
        Assert.DoesNotContain("testimize/generate_pairwise_test_cases", body);
    }

    [Fact]
    public void TestGeneration_Template_EmbedsPrecomputedDatasetWithAttribution()
    {
        var body = BuiltInTemplates.GetRawContent("test-generation")!;
        Assert.Contains("{{#if testimize_dataset}}", body);
        Assert.Contains("{{testimize_dataset}}", body);
        Assert.Contains("{{testimize_strategy_name}}", body);
        Assert.Contains("PRE-COMPUTED ALGORITHMIC TEST DATA", body);
        // Old MCP tool references must be gone.
        Assert.DoesNotContain("testimize/generate_hybrid_test_cases", body);
        Assert.DoesNotContain("testimize/generate_pairwise_test_cases", body);
    }

    [Fact]
    public void Resolve_TestimizeEnabledTrue_RendersFieldSpecsBlock()
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

        Assert.Contains("STRUCTURED FIELD SPECIFICATIONS", rendered);
        Assert.Contains("field_specs", rendered);
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

        Assert.DoesNotContain("STRUCTURED FIELD SPECIFICATIONS", rendered);
    }

    [Fact]
    public void Resolve_TestimizeDatasetEmpty_HidesEmbeddedBlock()
    {
        var template = BuiltInTemplates.GetTemplate("test-generation")!;
        var values = new Dictionary<string, string>
        {
            ["testimize_dataset"] = "",
            ["testimize_strategy_name"] = "",
            ["behaviors"] = "",
            ["suite_name"] = "",
            ["existing_tests"] = "",
            ["acceptance_criteria"] = "",
            ["profile_format"] = "",
            ["count"] = "1",
            ["focus_areas"] = ""
        };

        var rendered = PromptTemplateLoader.Resolve(template, values);

        Assert.DoesNotContain("PRE-COMPUTED ALGORITHMIC TEST DATA", rendered);
    }

    [Fact]
    public void Resolve_TestimizeDatasetNonEmpty_RendersEmbeddedBlock()
    {
        var template = BuiltInTemplates.GetTemplate("test-generation")!;
        var values = new Dictionary<string, string>
        {
            ["testimize_dataset"] = "```yaml\nstrategy: HybridArtificialBeeColony\n```",
            ["testimize_strategy_name"] = "HybridArtificialBeeColony",
            ["behaviors"] = "",
            ["suite_name"] = "",
            ["existing_tests"] = "",
            ["acceptance_criteria"] = "",
            ["profile_format"] = "",
            ["count"] = "1",
            ["focus_areas"] = ""
        };

        var rendered = PromptTemplateLoader.Resolve(template, values);

        Assert.Contains("PRE-COMPUTED ALGORITHMIC TEST DATA", rendered);
        Assert.Contains("HybridArtificialBeeColony", rendered);
    }
}
