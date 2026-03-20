using System.Text;
using System.Text.RegularExpressions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Updates YAML frontmatter fields in test Markdown files using targeted text insertion.
/// Preserves existing field ordering and custom fields.
/// </summary>
public sealed partial class FrontmatterUpdater
{
    [GeneratedRegex(@"^---\s*\n([\s\S]*?)\n---", RegexOptions.None)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"^automated_by:\s*.*$", RegexOptions.Multiline)]
    private static partial Regex AutomatedByLineRegex();

    [GeneratedRegex(@"^automated_by:\s*\n(?:^\s+-\s+.*\n)*", RegexOptions.Multiline)]
    private static partial Regex AutomatedByBlockRegex();

    /// <summary>
    /// Updates the automated_by field in a test file's YAML frontmatter.
    /// Returns the updated file content, or null if the file has no valid frontmatter.
    /// </summary>
    public string? UpdateAutomatedBy(string fileContent, IReadOnlyList<string> automationPaths)
    {
        var fmMatch = FrontmatterRegex().Match(fileContent);
        if (!fmMatch.Success)
        {
            return null;
        }

        var frontmatter = fmMatch.Groups[1].Value;
        var yamlValue = FormatYamlList("automated_by", automationPaths);

        string newFrontmatter;

        // Check if automated_by already exists as a block (list)
        var blockMatch = AutomatedByBlockRegex().Match(frontmatter);
        if (blockMatch.Success)
        {
            newFrontmatter = frontmatter[..blockMatch.Index]
                + yamlValue
                + frontmatter[(blockMatch.Index + blockMatch.Length)..];
        }
        else
        {
            // Check for inline automated_by
            var lineMatch = AutomatedByLineRegex().Match(frontmatter);
            if (lineMatch.Success)
            {
                newFrontmatter = frontmatter[..lineMatch.Index]
                    + yamlValue.TrimEnd('\n')
                    + frontmatter[(lineMatch.Index + lineMatch.Length)..];
            }
            else
            {
                // Insert before end of frontmatter
                newFrontmatter = frontmatter.TrimEnd() + "\n" + yamlValue.TrimEnd('\n');
            }
        }

        // Reconstruct file: replace old frontmatter section
        return fileContent[..fmMatch.Groups[1].Index]
            + newFrontmatter
            + fileContent[(fmMatch.Groups[1].Index + fmMatch.Groups[1].Length)..];
    }

    /// <summary>
    /// Updates the automated_by field in a file on disk.
    /// Returns true if the file was updated.
    /// </summary>
    public async Task<bool> UpdateFileAsync(
        string filePath,
        IReadOnlyList<string> automationPaths,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        var content = await File.ReadAllTextAsync(filePath, ct);
        var updated = UpdateAutomatedBy(content, automationPaths);

        if (updated is null || updated == content)
        {
            return false;
        }

        await File.WriteAllTextAsync(filePath, updated, ct);
        return true;
    }

    private static string FormatYamlList(string key, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return $"{key}: []\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{key}:");
        foreach (var value in values)
        {
            sb.AppendLine($"  - {value}");
        }
        return sb.ToString();
    }
}
