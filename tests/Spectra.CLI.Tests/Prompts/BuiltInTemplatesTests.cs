using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

public class BuiltInTemplatesTests
{
    [Fact]
    public void AllFiveTemplates_ExistAsEmbeddedResources()
    {
        var all = BuiltInTemplates.All;

        Assert.True(all.Count >= 5, $"Expected at least 5 built-in templates, got {all.Count}");
        Assert.True(all.ContainsKey("behavior-analysis"), "Missing behavior-analysis template");
        Assert.True(all.ContainsKey("test-generation"), "Missing test-generation template");
        Assert.True(all.ContainsKey("criteria-extraction"), "Missing criteria-extraction template");
        Assert.True(all.ContainsKey("critic-verification"), "Missing critic-verification template");
        Assert.True(all.ContainsKey("test-update"), "Missing test-update template");
    }

    [Theory]
    [InlineData("behavior-analysis")]
    [InlineData("test-generation")]
    [InlineData("criteria-extraction")]
    [InlineData("critic-verification")]
    [InlineData("test-update")]
    public void BuiltInTemplate_ParsesWithoutErrors(string templateId)
    {
        var template = BuiltInTemplates.GetTemplate(templateId);

        Assert.NotNull(template);
        Assert.Equal(templateId, template.TemplateId);
        Assert.Equal("1.0", template.SpectraVersion);
        Assert.False(string.IsNullOrWhiteSpace(template.Description));
        Assert.False(string.IsNullOrWhiteSpace(template.Body));
        Assert.NotEmpty(template.Placeholders);
        Assert.False(template.IsUserCustomized);
    }

    [Theory]
    [InlineData("behavior-analysis")]
    [InlineData("test-generation")]
    [InlineData("criteria-extraction")]
    [InlineData("critic-verification")]
    [InlineData("test-update")]
    public void BuiltInTemplate_HasValidSyntax(string templateId)
    {
        var template = BuiltInTemplates.GetTemplate(templateId);
        Assert.NotNull(template);

        var errors = PlaceholderResolver.ValidateSyntax(template.Body);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("behavior-analysis")]
    [InlineData("test-generation")]
    [InlineData("criteria-extraction")]
    [InlineData("critic-verification")]
    [InlineData("test-update")]
    public void BuiltInTemplate_RawContentIsNonEmpty(string templateId)
    {
        var raw = BuiltInTemplates.GetRawContent(templateId);

        Assert.NotNull(raw);
        Assert.True(raw.Length > 100, $"Template {templateId} is suspiciously short ({raw.Length} chars)");
    }

    [Fact]
    public void GetTemplate_InvalidId_ReturnsNull()
    {
        var template = BuiltInTemplates.GetTemplate("nonexistent-template");

        Assert.Null(template);
    }

    [Fact]
    public void AllTemplateIds_ContainsFiveEntries()
    {
        Assert.Equal(5, BuiltInTemplates.AllTemplateIds.Count);
    }
}
