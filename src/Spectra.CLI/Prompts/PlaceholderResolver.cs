using System.Text.RegularExpressions;

namespace Spectra.CLI.Prompts;

/// <summary>
/// Resolves {{var}}, {{#if var}}...{{/if}}, and {{#each var}}...{{/each}} placeholders in prompt templates.
/// </summary>
public static partial class PlaceholderResolver
{
    /// <summary>
    /// Resolves all placeholders in a template body using the provided values.
    /// List values for {{#each}} should be passed as a serialized string with items separated by the each-item format.
    /// </summary>
    public static string Resolve(string templateBody, IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? listValues = null)
    {
        var result = templateBody;

        // Strip HTML comments
        result = HtmlCommentRegex().Replace(result, "");

        // Process {{#each var}}...{{/each}} blocks
        result = EachBlockRegex().Replace(result, match =>
        {
            var varName = match.Groups[1].Value.Trim();
            var blockContent = match.Groups[2].Value;

            if (listValues is null || !listValues.TryGetValue(varName, out var items) || items.Count == 0)
                return "";

            var expanded = new System.Text.StringBuilder();
            foreach (var item in items)
            {
                var line = blockContent;
                foreach (var kvp in item)
                {
                    line = line.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
                }
                expanded.Append(line);
            }
            return expanded.ToString();
        });

        // Process {{#if var}}...{{/if}} blocks
        result = IfBlockRegex().Replace(result, match =>
        {
            var varName = match.Groups[1].Value.Trim();
            var blockContent = match.Groups[2].Value;

            if (values.TryGetValue(varName, out var value) && !string.IsNullOrEmpty(value))
                return blockContent;

            return "";
        });

        // Process simple {{var}} placeholders
        result = SimpleVarRegex().Replace(result, match =>
        {
            var varName = match.Groups[1].Value.Trim();
            return values.TryGetValue(varName, out var value) ? value : "";
        });

        return result;
    }

    /// <summary>
    /// Validates template syntax: checks for unclosed blocks and returns errors.
    /// </summary>
    public static IReadOnlyList<string> ValidateSyntax(string templateBody)
    {
        var errors = new List<string>();

        // Check for unclosed {{#if}} blocks
        var ifOpens = IfOpenRegex().Matches(templateBody).Count;
        var ifCloses = IfCloseRegex().Matches(templateBody).Count;
        if (ifOpens != ifCloses)
            errors.Add($"Mismatched {{{{#if}}}} blocks: {ifOpens} opens, {ifCloses} closes");

        // Check for unclosed {{#each}} blocks
        var eachOpens = EachOpenRegex().Matches(templateBody).Count;
        var eachCloses = EachCloseRegex().Matches(templateBody).Count;
        if (eachOpens != eachCloses)
            errors.Add($"Mismatched {{{{#each}}}} blocks: {eachOpens} opens, {eachCloses} closes");

        // Check for nested control blocks (not supported)
        var eachMatches = EachBlockRegex().Matches(templateBody);
        foreach (Match eachMatch in eachMatches)
        {
            var innerContent = eachMatch.Groups[2].Value;
            if (IfOpenRegex().IsMatch(innerContent))
                errors.Add("Nested {{#if}} inside {{#each}} is not supported");
            if (EachOpenRegex().IsMatch(innerContent))
                errors.Add("Nested {{#each}} inside {{#each}} is not supported");
        }

        var ifMatches = IfBlockRegex().Matches(templateBody);
        foreach (Match ifMatch in ifMatches)
        {
            var innerContent = ifMatch.Groups[2].Value;
            if (IfOpenRegex().IsMatch(innerContent))
                errors.Add("Nested {{#if}} inside {{#if}} is not supported");
            if (EachOpenRegex().IsMatch(innerContent))
                errors.Add("Nested {{#each}} inside {{#if}} is not supported");
        }

        return errors;
    }

    /// <summary>
    /// Extracts all placeholder names used in a template body.
    /// </summary>
    public static IReadOnlyList<string> ExtractPlaceholderNames(string templateBody)
    {
        var names = new HashSet<string>();

        foreach (Match match in SimpleVarRegex().Matches(templateBody))
            names.Add(match.Groups[1].Value.Trim());

        foreach (Match match in IfOpenRegex().Matches(templateBody))
            names.Add(match.Groups[1].Value.Trim());

        foreach (Match match in EachOpenRegex().Matches(templateBody))
            names.Add(match.Groups[1].Value.Trim());

        return names.ToList();
    }

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"\{\{#each\s+(\w+)\}\}([\s\S]*?)\{\{/each\}\}")]
    private static partial Regex EachBlockRegex();

    [GeneratedRegex(@"\{\{#if\s+(\w+)\}\}([\s\S]*?)\{\{/if\}\}")]
    private static partial Regex IfBlockRegex();

    [GeneratedRegex(@"\{\{(?!#if\s)(?!#each\s)(?!/if)(?!/each)(\w+)\}\}")]
    private static partial Regex SimpleVarRegex();

    [GeneratedRegex(@"\{\{#if\s+(\w+)\}\}")]
    private static partial Regex IfOpenRegex();

    [GeneratedRegex(@"\{\{/if\}\}")]
    private static partial Regex IfCloseRegex();

    [GeneratedRegex(@"\{\{#each\s+(\w+)\}\}")]
    private static partial Regex EachOpenRegex();

    [GeneratedRegex(@"\{\{/each\}\}")]
    private static partial Regex EachCloseRegex();
}
