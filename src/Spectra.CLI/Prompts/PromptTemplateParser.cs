using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Models;

namespace Spectra.CLI.Prompts;

/// <summary>
/// Parses markdown files with YAML frontmatter into PromptTemplate records.
/// </summary>
public static partial class PromptTemplateParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parses a prompt template from markdown content with YAML frontmatter.
    /// Returns null if parsing fails.
    /// </summary>
    public static PromptTemplate? Parse(string content, bool isUserCustomized = false)
    {
        try
        {
            var (frontmatter, body) = SplitFrontmatter(content);
            if (frontmatter is null || body is null)
                return null;

            var metadata = ParseFrontmatter(frontmatter);
            if (metadata is null)
                return null;

            return new PromptTemplate
            {
                SpectraVersion = metadata.SpectraVersion ?? "1.0",
                TemplateId = metadata.TemplateId ?? "",
                Description = metadata.Description ?? "",
                Placeholders = metadata.Placeholders ?? [],
                Body = body.Trim(),
                IsUserCustomized = isUserCustomized
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Splits content into frontmatter and body sections using --- delimiters.
    /// </summary>
    internal static (string? frontmatter, string? body) SplitFrontmatter(string content)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("---"))
            return (null, null);

        // Find the closing ---
        var endIndex = trimmed.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, null);

        var frontmatter = trimmed[3..endIndex].Trim();
        var body = trimmed[(endIndex + 4)..]; // skip \n---

        return (frontmatter, body);
    }

    /// <summary>
    /// Parses YAML frontmatter into metadata. Uses simple key-value parsing
    /// to avoid a YamlDotNet dependency.
    /// </summary>
    internal static TemplateMetadata? ParseFrontmatter(string yaml)
    {
        var metadata = new TemplateMetadata();
        var lines = yaml.Split('\n');
        var placeholders = new List<PlaceholderSpec>();
        var inPlaceholders = false;
        string? currentPlaceholderName = null;
        string? currentPlaceholderDescription = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Skip comments
            if (line.TrimStart().StartsWith('#'))
                continue;

            // Check if we're in placeholders list
            if (line.TrimStart().StartsWith("placeholders:"))
            {
                inPlaceholders = true;
                continue;
            }

            if (inPlaceholders)
            {
                var trimmedLine = line.TrimStart();

                // New list item
                if (trimmedLine.StartsWith("- "))
                {
                    // Save previous placeholder
                    FlushPlaceholder(placeholders, ref currentPlaceholderName, ref currentPlaceholderDescription);

                    var itemContent = trimmedLine[2..].Trim();
                    if (itemContent.StartsWith("name:"))
                    {
                        currentPlaceholderName = ExtractValue(itemContent["name:".Length..]);
                    }
                    continue;
                }

                // Continuation of current list item
                if (trimmedLine.StartsWith("name:"))
                {
                    currentPlaceholderName = ExtractValue(trimmedLine["name:".Length..]);
                    continue;
                }

                if (trimmedLine.StartsWith("description:"))
                {
                    currentPlaceholderDescription = ExtractValue(trimmedLine["description:".Length..]);
                    continue;
                }

                // Non-indented line means end of placeholders
                if (!string.IsNullOrWhiteSpace(line) && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("-"))
                {
                    inPlaceholders = false;
                    FlushPlaceholder(placeholders, ref currentPlaceholderName, ref currentPlaceholderDescription);
                }
                else
                {
                    continue;
                }
            }

            // Top-level key-value pairs
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0 && !char.IsWhiteSpace(line[0]))
            {
                var key = line[..colonIndex].Trim();
                var value = ExtractValue(line[(colonIndex + 1)..]);

                switch (key)
                {
                    case "spectra_version":
                        metadata.SpectraVersion = value;
                        break;
                    case "template_id":
                        metadata.TemplateId = value;
                        break;
                    case "description":
                        metadata.Description = value;
                        break;
                }
            }
        }

        // Flush last placeholder
        FlushPlaceholder(placeholders, ref currentPlaceholderName, ref currentPlaceholderDescription);

        metadata.Placeholders = placeholders;
        return metadata;
    }

    private static void FlushPlaceholder(List<PlaceholderSpec> list, ref string? name, ref string? description)
    {
        if (name is not null)
        {
            list.Add(new PlaceholderSpec { Name = name, Description = description });
        }
        name = null;
        description = null;
    }

    private static string ExtractValue(string raw)
    {
        var value = raw.Trim();
        // Remove surrounding quotes
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];
        return value;
    }

    internal sealed class TemplateMetadata
    {
        public string? SpectraVersion { get; set; }
        public string? TemplateId { get; set; }
        public string? Description { get; set; }
        public List<PlaceholderSpec>? Placeholders { get; set; }
    }
}
