using Spectra.Core.Models;

namespace Spectra.Core.Tests.Models;

public class PromptTemplateTests
{
    [Fact]
    public void PromptTemplate_CanBeCreated()
    {
        var template = new PromptTemplate
        {
            SpectraVersion = "1.0",
            TemplateId = "test-template",
            Description = "A test template",
            Placeholders =
            [
                new PlaceholderSpec { Name = "var1", Description = "First variable" },
                new PlaceholderSpec { Name = "var2" }
            ],
            Body = "Hello {{var1}} and {{var2}}!",
            IsUserCustomized = true
        };

        Assert.Equal("1.0", template.SpectraVersion);
        Assert.Equal("test-template", template.TemplateId);
        Assert.Equal("A test template", template.Description);
        Assert.Equal(2, template.Placeholders.Count);
        Assert.Equal("var1", template.Placeholders[0].Name);
        Assert.Equal("First variable", template.Placeholders[0].Description);
        Assert.Null(template.Placeholders[1].Description);
        Assert.Contains("{{var1}}", template.Body);
        Assert.True(template.IsUserCustomized);
    }

    [Fact]
    public void PromptTemplate_DefaultIsUserCustomized_IsFalse()
    {
        var template = new PromptTemplate
        {
            SpectraVersion = "1.0",
            TemplateId = "test",
            Description = "test",
            Placeholders = [],
            Body = "test body"
        };

        Assert.False(template.IsUserCustomized);
    }

    [Fact]
    public void PlaceholderSpec_HasNameAndOptionalDescription()
    {
        var spec = new PlaceholderSpec { Name = "focus_areas" };

        Assert.Equal("focus_areas", spec.Name);
        Assert.Null(spec.Description);
    }
}
