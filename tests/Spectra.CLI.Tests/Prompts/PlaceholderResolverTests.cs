using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

public class PlaceholderResolverTests
{
    [Fact]
    public void Resolve_SimpleVar_ReplacesWithValue()
    {
        var template = "Hello {{name}}, welcome to {{project}}!";
        var values = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["project"] = "SPECTRA"
        };

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.Equal("Hello Alice, welcome to SPECTRA!", result);
    }

    [Fact]
    public void Resolve_MissingVar_ReplacesWithEmptyString()
    {
        var template = "Hello {{name}}!";
        var values = new Dictionary<string, string>();

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.Equal("Hello !", result);
    }

    [Fact]
    public void Resolve_IfBlock_IncludedWhenNonEmpty()
    {
        var template = "Start {{#if focus}}Focus: {{focus}}{{/if}} End";
        var values = new Dictionary<string, string> { ["focus"] = "security" };

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.Equal("Start Focus: security End", result);
    }

    [Fact]
    public void Resolve_IfBlock_ExcludedWhenEmpty()
    {
        var template = "Start {{#if focus}}Focus: {{focus}}{{/if}} End";
        var values = new Dictionary<string, string> { ["focus"] = "" };

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.Equal("Start  End", result);
    }

    [Fact]
    public void Resolve_IfBlock_ExcludedWhenMissing()
    {
        var template = "Start {{#if focus}}Focus: {{focus}}{{/if}} End";
        var values = new Dictionary<string, string>();

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.Equal("Start  End", result);
    }

    [Fact]
    public void Resolve_EachBlock_ExpandsList()
    {
        var template = "Categories:\n{{#each categories}}- {{id}}: {{description}}\n{{/each}}Done";
        var values = new Dictionary<string, string>();
        var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["categories"] = new List<IReadOnlyDictionary<string, string>>
            {
                new Dictionary<string, string> { ["id"] = "happy_path", ["description"] = "Normal flows" },
                new Dictionary<string, string> { ["id"] = "negative", ["description"] = "Error handling" }
            }
        };

        var result = PlaceholderResolver.Resolve(template, values, listValues);

        Assert.Contains("- happy_path: Normal flows", result);
        Assert.Contains("- negative: Error handling", result);
        Assert.EndsWith("Done", result);
    }

    [Fact]
    public void Resolve_EachBlock_EmptyList_RemovesBlock()
    {
        var template = "Before {{#each items}}Item: {{id}}\n{{/each}}After";
        var values = new Dictionary<string, string>();
        var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["items"] = new List<IReadOnlyDictionary<string, string>>()
        };

        var result = PlaceholderResolver.Resolve(template, values, listValues);

        Assert.Equal("Before After", result);
    }

    [Fact]
    public void Resolve_EachBlock_MissingListValue_RemovesBlock()
    {
        var template = "Before {{#each items}}Item: {{id}}\n{{/each}}After";
        var values = new Dictionary<string, string>();

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.Equal("Before After", result);
    }

    [Fact]
    public void Resolve_HtmlComments_AreStripped()
    {
        var template = "<!-- This is a comment -->\nHello {{name}}!";
        var values = new Dictionary<string, string> { ["name"] = "World" };

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.DoesNotContain("<!--", result);
        Assert.DoesNotContain("comment", result);
        Assert.Contains("Hello World!", result);
    }

    [Fact]
    public void Resolve_MultiLineHtmlComment_Stripped()
    {
        var template = "Start\n<!-- \nMulti\nline\ncomment\n-->\nEnd";
        var values = new Dictionary<string, string>();

        var result = PlaceholderResolver.Resolve(template, values);

        Assert.DoesNotContain("Multi", result);
        Assert.Contains("Start", result);
        Assert.Contains("End", result);
    }

    [Fact]
    public void ValidateSyntax_ValidTemplate_ReturnsNoErrors()
    {
        var template = "{{#if focus}}Focus: {{focus}}{{/if}} {{#each categories}}{{id}}{{/each}}";

        var errors = PlaceholderResolver.ValidateSyntax(template);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSyntax_UnclosedIf_ReturnsError()
    {
        var template = "{{#if focus}}Focus: {{focus}}";

        var errors = PlaceholderResolver.ValidateSyntax(template);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("#if"));
    }

    [Fact]
    public void ValidateSyntax_UnclosedEach_ReturnsError()
    {
        var template = "{{#each items}}Item: {{id}}";

        var errors = PlaceholderResolver.ValidateSyntax(template);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("#each"));
    }

    [Fact]
    public void ExtractPlaceholderNames_FindsAll()
    {
        var template = "{{name}} {{#if focus}}{{focus}}{{/if}} {{#each categories}}{{id}}{{/each}}";

        var names = PlaceholderResolver.ExtractPlaceholderNames(template);

        Assert.Contains("name", names);
        Assert.Contains("focus", names);
        Assert.Contains("categories", names);
    }
}
