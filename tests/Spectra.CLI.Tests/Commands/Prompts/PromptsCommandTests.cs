using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Commands.Prompts;

public class PromptsCommandTests
{
    [Fact]
    public void AllTemplateIds_Has5Entries()
    {
        Assert.Equal(5, BuiltInTemplates.AllTemplateIds.Count);
    }

    [Fact]
    public void AllTemplateIds_ContainsExpectedIds()
    {
        var expected = new[]
        {
            "behavior-analysis",
            "test-generation",
            "criteria-extraction",
            "critic-verification",
            "test-update"
        };

        foreach (var id in expected)
            Assert.Contains(id, BuiltInTemplates.AllTemplateIds);
    }

    [Theory]
    [InlineData("behavior-analysis", "document_text")]
    [InlineData("test-generation", "count")]
    [InlineData("criteria-extraction", "document_text")]
    [InlineData("critic-verification", "test_case")]
    [InlineData("test-update", "test_case")]
    public void Template_ContainsExpectedPlaceholder(string templateId, string expectedPlaceholder)
    {
        var template = BuiltInTemplates.GetTemplate(templateId);
        Assert.NotNull(template);
        Assert.Contains(template.Placeholders, p => p.Name == expectedPlaceholder);
    }

    [Fact]
    public void DefaultCategories_Has6Entries()
    {
        Assert.Equal(6, DefaultCategories.All.Count);
    }

    [Fact]
    public void DefaultCategories_ContainsExpectedIds()
    {
        var ids = DefaultCategories.All.Select(c => c.Id).ToList();
        Assert.Contains("happy_path", ids);
        Assert.Contains("negative", ids);
        Assert.Contains("edge_case", ids);
        Assert.Contains("boundary", ids);
        Assert.Contains("error_handling", ids);
        Assert.Contains("security", ids);
    }

    [Fact]
    public void SkillContent_ContainsPromptsSkill()
    {
        var skills = Spectra.CLI.Skills.SkillContent.All;
        Assert.True(skills.ContainsKey("spectra-prompts"), "spectra-prompts SKILL not found");
        Assert.Contains("spectra prompts list", skills["spectra-prompts"]);
    }

    [Fact]
    public void SkillContent_Has11Skills()
    {
        var skills = Spectra.CLI.Skills.SkillContent.All;
        Assert.True(skills.Count >= 11, $"Expected at least 11 SKILLs, got {skills.Count}");
    }
}
