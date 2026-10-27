using Spectra.CLI.Generation;
using Spectra.CLI.Prompts;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 094 T6 (FR-012; US4): no generation prompt names the four tools that have not existed since spec 069 or asks
/// to read docs "ON-DEMAND" — the shipped template, count mode, from-description and the built-in fallback. Count mode
/// says to read the documentation with the file tools and that ids are assigned when the tests are ingested. On 55b541f
/// all of them carried the dead tool workflow.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GenerationTemplateToolTextTests : IDisposable
{
    private static readonly string[] DeadText =
        ["ON-DEMAND", "ListDocumentationFiles", "ReadTestIndex", "CheckDuplicates", "GetNextTestIds", "Use the tools"];

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public GenerationTemplateToolTextTests() => SetUpAsync(_ws).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private static void AssertCountModeText(string prompt)
    {
        foreach (var text in DeadText)
        {
            Assert.DoesNotContain(text, prompt);
        }

        Assert.Contains("read the relevant documentation with your file tools", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assigned when the tests are ingested", prompt);
    }

    [Fact]
    public void TheShippedTemplate_HasNoDeadToolText_AndDeclaresTheBatchPlaceholders()
    {
        var raw = BuiltInTemplates.GetRawContent("test-generation")!;

        foreach (var text in DeadText)
        {
            Assert.DoesNotContain(text, raw);
        }

        var template = BuiltInTemplates.GetTemplate("test-generation")!;
        Assert.Contains(template.Placeholders, p => p.Name == "source_sections");
        Assert.Contains(template.Placeholders, p => p.Name == "read_docs");
        Assert.Empty(PlaceholderResolver.ValidateSyntax(template.Body));
    }

    [Fact]
    public async Task CountMode_SaysToReadTheDocsWithTheFileTools_AndThatIdsComeFromTheIngest()
    {
        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--count", "3");

        Assert.True(exit == 0, error);
        AssertCountModeText(prompt);
    }

    [Fact]
    public async Task FromDescription_HasNoDeadToolText()
    {
        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--from-description", "Upload a 31 GB file");

        Assert.True(exit == 0, error);
        AssertCountModeText(prompt);
        Assert.Contains("Upload a 31 GB file", prompt);
    }

    [Fact]
    public void TheBuiltInFallback_HasNoDeadToolText()
    {
        var prompt = PromptCompiler.Assemble("Generate tests for storage", 3, "- **AC-1** [MUST] x\n", templateLoader: null, profileFormat: "[]");

        AssertCountModeText(prompt);
        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", prompt);
    }

    [Fact]
    public async Task CountModeRefusal_KeepsItsPre094Shape()
    {
        foreach (var file in Directory.GetFiles(_ws.PathOf("docs/criteria"), "*.criteria.yaml", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        var (exit, output, error) = await _ws.CompilePromptAsync("--suite", "storage", "--count", "3");

        Assert.Equal(4, exit);
        Assert.Equal("", output);
        var refusal = BaselineTestWorkspace.Json(error.Trim());
        Assert.True(refusal.GetProperty("refused").GetBoolean());
        Assert.Equal("criteria_context", refusal.GetProperty("missing_input").GetString());
    }
}
