using Spectra.CLI.Prompts;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Prompts;

public class PromptTemplateLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public PromptTemplateLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void LoadTemplate_MissingFile_ReturnsBuiltIn()
    {
        var loader = new PromptTemplateLoader(_tempDir);

        var template = loader.LoadTemplate("behavior-analysis");

        Assert.NotNull(template);
        Assert.False(template.IsUserCustomized);
        Assert.Equal("behavior-analysis", template.TemplateId);
    }

    [Fact]
    public void LoadTemplate_UserFile_Preferred()
    {
        var promptsDir = Path.Combine(_tempDir, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);
        File.WriteAllText(Path.Combine(promptsDir, "behavior-analysis.md"), """
            ---
            spectra_version: "1.0"
            template_id: behavior-analysis
            description: "Custom template"
            placeholders:
              - name: document_text
            ---
            Custom analysis prompt: {{document_text}}
            """);

        var loader = new PromptTemplateLoader(_tempDir);

        var template = loader.LoadTemplate("behavior-analysis");

        Assert.NotNull(template);
        Assert.True(template.IsUserCustomized);
        Assert.Contains("Custom analysis prompt", template.Body);
    }

    [Fact]
    public void LoadTemplate_InvalidUserFile_FallsBackToBuiltIn()
    {
        var promptsDir = Path.Combine(_tempDir, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);
        // Write invalid YAML (no frontmatter delimiters)
        File.WriteAllText(Path.Combine(promptsDir, "behavior-analysis.md"), "This is not a valid template");

        var loader = new PromptTemplateLoader(_tempDir);

        var template = loader.LoadTemplate("behavior-analysis");

        Assert.NotNull(template);
        Assert.False(template.IsUserCustomized);
    }

    [Fact]
    public void LoadTemplate_EmptyUserFile_FallsBackToBuiltIn()
    {
        var promptsDir = Path.Combine(_tempDir, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);
        File.WriteAllText(Path.Combine(promptsDir, "behavior-analysis.md"), "");

        var loader = new PromptTemplateLoader(_tempDir);

        var template = loader.LoadTemplate("behavior-analysis");

        Assert.NotNull(template);
        Assert.False(template.IsUserCustomized);
    }

    [Fact]
    public void GetTemplateStatus_MissingFile_ReturnsMissing()
    {
        var loader = new PromptTemplateLoader(_tempDir);

        var status = loader.GetTemplateStatus("behavior-analysis");

        Assert.Equal(TemplateFileStatus.Missing, status);
    }

    [Fact]
    public void GetTemplateStatus_DefaultFile_ReturnsDefault()
    {
        var promptsDir = Path.Combine(_tempDir, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);

        var builtInContent = BuiltInTemplates.GetRawContent("behavior-analysis");
        Assert.NotNull(builtInContent);
        File.WriteAllText(Path.Combine(promptsDir, "behavior-analysis.md"), builtInContent);

        var loader = new PromptTemplateLoader(_tempDir);

        var status = loader.GetTemplateStatus("behavior-analysis");

        Assert.Equal(TemplateFileStatus.Default, status);
    }

    [Fact]
    public void GetTemplateStatus_CustomizedFile_ReturnsCustomized()
    {
        var promptsDir = Path.Combine(_tempDir, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);
        File.WriteAllText(Path.Combine(promptsDir, "behavior-analysis.md"), """
            ---
            spectra_version: "1.0"
            template_id: behavior-analysis
            description: "Custom"
            placeholders:
              - name: document_text
            ---
            My custom prompt
            """);

        var loader = new PromptTemplateLoader(_tempDir);

        var status = loader.GetTemplateStatus("behavior-analysis");

        Assert.Equal(TemplateFileStatus.Customized, status);
    }

    [Fact]
    public void GetCategories_WithConfig_ReturnsConfigCategories()
    {
        var config = new SpectraConfig
        {
            Source = new SourceConfig(),
            Tests = new TestsConfig(),
            Ai = new AiConfig { Providers = [] },
            Analysis = new AnalysisConfig
            {
                Categories =
                [
                    new CategoryDefinition { Id = "compliance", Description = "Regulatory checks" }
                ]
            }
        };

        var categories = PromptTemplateLoader.GetCategories(config);

        Assert.Single(categories);
        Assert.Equal("compliance", categories[0].Id);
    }

    [Fact]
    public void GetCategories_EmptyConfig_ReturnsDefaults()
    {
        var config = new SpectraConfig
        {
            Source = new SourceConfig(),
            Tests = new TestsConfig(),
            Ai = new AiConfig { Providers = [] }
        };

        var categories = PromptTemplateLoader.GetCategories(config);

        Assert.Equal(DefaultCategories.All.Count, categories.Count);
    }

    [Fact]
    public void GetCategories_NullConfig_ReturnsDefaults()
    {
        var categories = PromptTemplateLoader.GetCategories(null);

        Assert.Equal(DefaultCategories.All.Count, categories.Count);
    }

    [Fact]
    public void FormatCategoriesForTemplate_FormatsCorrectly()
    {
        var categories = new List<CategoryDefinition>
        {
            new() { Id = "security", Description = "Auth checks" }
        };

        var formatted = PromptTemplateLoader.FormatCategoriesForTemplate(categories);

        Assert.Single(formatted);
        Assert.Equal("security", formatted[0]["id"]);
        Assert.Equal("Auth checks", formatted[0]["description"]);
    }
}
