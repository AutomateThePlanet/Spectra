using System.Reflection;
using Spectra.Core.Models;

namespace Spectra.CLI.Prompts;

/// <summary>
/// Provides access to built-in prompt templates embedded as assembly resources.
/// </summary>
public static class BuiltInTemplates
{
    private const string ResourcePrefix = "Spectra.CLI.Prompts.Content.";

    /// <summary>
    /// All known template IDs.
    /// </summary>
    public static readonly IReadOnlyList<string> AllTemplateIds =
    [
        "behavior-analysis",
        "test-generation",
        "criteria-extraction",
        "critic-verification",
        "test-update"
    ];

    private static readonly Lazy<Dictionary<string, string>> CachedContent = new(LoadAll);

    /// <summary>
    /// Gets the raw content of all built-in templates, keyed by template ID.
    /// </summary>
    public static IReadOnlyDictionary<string, string> All => CachedContent.Value;

    /// <summary>
    /// Gets a parsed built-in template by ID. Returns null if not found.
    /// </summary>
    public static PromptTemplate? GetTemplate(string templateId)
    {
        if (!CachedContent.Value.TryGetValue(templateId, out var content))
            return null;

        return PromptTemplateParser.Parse(content, isUserCustomized: false);
    }

    /// <summary>
    /// Gets the raw content of a built-in template by ID. Returns null if not found.
    /// </summary>
    public static string? GetRawContent(string templateId)
    {
        return CachedContent.Value.TryGetValue(templateId, out var content) ? content : null;
    }

    private static Dictionary<string, string> LoadAll()
    {
        var result = new Dictionary<string, string>();
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;

            var templateId = name[ResourcePrefix.Length..];
            if (templateId.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                templateId = templateId[..^3];

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;

            using var reader = new StreamReader(stream);
            result[templateId] = reader.ReadToEnd();
        }

        return result;
    }
}
