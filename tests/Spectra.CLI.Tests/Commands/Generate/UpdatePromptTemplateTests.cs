using Spectra.CLI.Prompts;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.UpdatePromptWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 088 T4 (FR-011, FR-012): the built-in <c>test-update</c> template uses the new placeholders; a custom template
/// written before 088 still works for one test (with a warning), and is refused for a group.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdatePromptTemplateTests : IDisposable
{
    // The 087 template's placeholders and shape, as a user copy would still have them.
    private const string Pre088Template =
        "---\nspectra_version: \"1.0\"\ntemplate_id: test-update\ndescription: \"old\"\nplaceholders:\n"
        + "  - name: test_case\n  - name: current_source\n  - name: acceptance_criteria\n  - name: profile_format\n---\n\n"
        + "EDIT — DO NOT REGENERATE (custom)\n\n### EXISTING TEST (edit this)\n```json\n{{test_case}}\n```\n\n"
        + "{{#if current_source}}\n### CURRENT SOURCE\n{{current_source}}\n{{/if}}\n\n"
        + "{{#if acceptance_criteria}}\n### ACCEPTANCE CRITERIA\n{{acceptance_criteria}}\n{{/if}}\n\n"
        + "## OUTPUT FORMAT\n```json\n{{profile_format}}\n```\n";

    private readonly BaselineTestWorkspace _ws = new();

    public UpdatePromptTemplateTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
    }

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void TheBuiltInTemplate_DeclaresAndUsesTheNewPlaceholders()
    {
        var template = BuiltInTemplates.GetTemplate("test-update")!;

        foreach (var name in new[] { "source_diff", "acceptance_criteria", "tests", "profile_format" })
        {
            Assert.Contains(template.Placeholders, p => p.Name == name);
        }

        Assert.Contains("{{source_diff}}", template.Body);
        Assert.Contains("{{tests}}", template.Body);
        Assert.Contains("returned unchanged", template.Body);
        Assert.Contains("ONE object per `### TEST`", template.Body);
    }

    [Fact]
    public async Task APre088CustomTemplate_TestIdWorksWithAWarning_GroupIsRefused()
    {
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
        Directory.CreateDirectory(_ws.PathOf(".spectra/prompts"));
        File.WriteAllText(_ws.PathOf(".spectra/prompts/test-update.md"), Pre088Template);
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-101").GetProperty("id").GetString()!;

        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.True(exit == 0, error);
        Assert.Contains("EDIT — DO NOT REGENERATE (custom)", prompt);
        Assert.Contains("**45GB**", prompt); // the section reaches the old template through current_source
        Assert.Contains("\"id\": \"TC-101\"", prompt);
        Assert.Contains("spectra prompts reset test-update", error);

        var (groupExit, output, groupError) = await _ws.CompileJsonAsync("--suite", "storage", "--group", group);

        Assert.Equal(4, groupExit);
        Assert.Empty(output);
        var failure = Failure(groupError);
        Assert.Equal("TEMPLATE_OUTDATED", failure.GetProperty("error_code").GetString());
        Assert.Contains("spectra prompts reset test-update", failure.GetProperty("errors")[0].GetString());
    }
}
