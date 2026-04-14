using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

public sealed class PromptTemplateCoverageTests
{
    [Fact]
    public void CoverageContextPlaceholder_ResolvesWithoutErrors()
    {
        // Load the built-in behavior-analysis template
        var template = BuiltInTemplates.GetTemplate("behavior-analysis");
        Assert.NotNull(template);

        // Verify template contains the coverage_context placeholder
        Assert.Contains("coverage_context", template.Body);

        // Resolve with all placeholders including coverage_context
        var values = new Dictionary<string, string>
        {
            ["testimize_enabled"] = "",
            ["document_text"] = "Sample doc content",
            ["document_title"] = "Test Doc",
            ["suite_name"] = "test-suite",
            ["existing_tests"] = "",
            ["focus_areas"] = "",
            ["acceptance_criteria"] = "",
            ["coverage_context"] = "## EXISTING COVERAGE\nThis suite has 231 tests."
        };

        var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["categories"] = new List<IReadOnlyDictionary<string, string>>
            {
                new Dictionary<string, string> { ["id"] = "happy_path", ["description"] = "Happy path" }
            }
        };

        var resolved = PlaceholderResolver.Resolve(template.Body, values, listValues);

        Assert.Contains("EXISTING COVERAGE", resolved);
        Assert.Contains("231 tests", resolved);
    }

    [Fact]
    public void CoverageContextPlaceholder_EmptyValue_OmitsSection()
    {
        var template = BuiltInTemplates.GetTemplate("behavior-analysis");
        Assert.NotNull(template);

        var values = new Dictionary<string, string>
        {
            ["testimize_enabled"] = "",
            ["document_text"] = "Sample doc",
            ["document_title"] = "Test",
            ["suite_name"] = "",
            ["existing_tests"] = "",
            ["focus_areas"] = "",
            ["acceptance_criteria"] = "",
            ["coverage_context"] = ""
        };

        var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["categories"] = new List<IReadOnlyDictionary<string, string>>
            {
                new Dictionary<string, string> { ["id"] = "happy_path", ["description"] = "Happy path" }
            }
        };

        var resolved = PlaceholderResolver.Resolve(template.Body, values, listValues);

        // Empty coverage_context means no coverage section
        Assert.DoesNotContain("EXISTING COVERAGE", resolved);
    }
}
