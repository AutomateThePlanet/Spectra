using Microsoft.Extensions.Logging;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Prompts;

/// <summary>
/// Loads prompt templates from user files (.spectra/prompts/) with fallback to built-in defaults.
/// </summary>
public sealed class PromptTemplateLoader
{
    private readonly string _promptsDir;
    private readonly ILogger? _logger;

    public PromptTemplateLoader(string workingDirectory, ILogger? logger = null)
    {
        _promptsDir = Path.Combine(workingDirectory, ".spectra", "prompts");
        _logger = logger;
    }

    /// <summary>
    /// Load a prompt template by ID. Returns user template if exists and is valid,
    /// otherwise returns built-in default.
    /// </summary>
    public PromptTemplate LoadTemplate(string templateId)
    {
        // Try user file first
        var userFilePath = Path.Combine(_promptsDir, $"{templateId}.md");
        if (File.Exists(userFilePath))
        {
            try
            {
                var content = File.ReadAllText(userFilePath);
                var template = PromptTemplateParser.Parse(content, isUserCustomized: true);
                if (template is not null && !string.IsNullOrWhiteSpace(template.Body))
                    return template;

                _logger?.LogWarning("User template '{TemplateId}' failed to parse, using built-in default", templateId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error reading user template '{TemplateId}', using built-in default", templateId);
            }
        }

        // Fall back to built-in
        var builtIn = BuiltInTemplates.GetTemplate(templateId);
        if (builtIn is not null)
            return builtIn;

        // Should never happen if template ID is valid
        throw new InvalidOperationException($"Built-in template '{templateId}' not found");
    }

    /// <summary>
    /// Resolve all placeholders in a template with provided values.
    /// </summary>
    public static string Resolve(PromptTemplate template, IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? listValues = null)
    {
        return PlaceholderResolver.Resolve(template.Body, values, listValues);
    }

    /// <summary>
    /// Checks if a user template exists and is customized (different from built-in).
    /// </summary>
    public TemplateFileStatus GetTemplateStatus(string templateId)
    {
        var userFilePath = Path.Combine(_promptsDir, $"{templateId}.md");

        if (!File.Exists(userFilePath))
            return TemplateFileStatus.Missing;

        var userContent = File.ReadAllText(userFilePath);
        var userHash = FileHasher.ComputeHash(userContent);

        var builtInContent = BuiltInTemplates.GetRawContent(templateId);
        if (builtInContent is null)
            return TemplateFileStatus.Customized;

        var builtInHash = FileHasher.ComputeHash(builtInContent);
        return userHash == builtInHash ? TemplateFileStatus.Default : TemplateFileStatus.Customized;
    }

    /// <summary>
    /// Gets the categories to use: from config if present, otherwise defaults.
    /// </summary>
    public static IReadOnlyList<CategoryDefinition> GetCategories(SpectraConfig? config)
    {
        var configCategories = config?.Analysis.Categories;
        if (configCategories is not null && configCategories.Count > 0)
            return configCategories;

        return DefaultCategories.All;
    }

    /// <summary>
    /// Formats categories as list values for the {{#each categories}} placeholder.
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> FormatCategoriesForTemplate(
        IReadOnlyList<CategoryDefinition> categories)
    {
        return categories.Select(c => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
        {
            ["id"] = c.Id,
            ["description"] = c.Description
        }).ToList();
    }
}

public enum TemplateFileStatus
{
    Default,
    Customized,
    Missing
}
