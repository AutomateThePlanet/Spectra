using YamlDotNet.Serialization;

namespace Spectra.CLI.Source;

/// <summary>
/// Lightweight YAML-frontmatter reader for source documents (Spec 040 §3.5 / Phase 5).
/// Handles only the fields the indexer cares about: <c>suite:</c> (per-doc
/// override of the directory-default suite assignment).
/// </summary>
internal static class FrontmatterReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Returns the value of a top-level <c>suite:</c> field in the document's
    /// YAML frontmatter, or null when no frontmatter is present, no
    /// <c>suite</c> key is set, or the frontmatter is malformed.
    /// </summary>
    public static string? ReadSuite(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;
        if (!content.StartsWith("---", StringComparison.Ordinal)) return null;

        // Skip the leading delimiter line; find the closing one.
        var firstNewline = content.IndexOf('\n');
        if (firstNewline < 0) return null;

        var closeIdx = content.IndexOf("\n---", firstNewline + 1, StringComparison.Ordinal);
        if (closeIdx < 0) return null;

        var fmText = content.Substring(firstNewline + 1, closeIdx - firstNewline - 1);
        if (string.IsNullOrWhiteSpace(fmText)) return null;

        try
        {
            var dict = Deserializer.Deserialize<Dictionary<string, object?>>(fmText);
            if (dict is null) return null;
            if (dict.TryGetValue("suite", out var raw) && raw is not null)
            {
                var s = raw.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
        }
        catch
        {
            // Malformed frontmatter — treat as absent.
        }

        return null;
    }
}
