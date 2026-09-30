using System.Net;
using System.Text.RegularExpressions;

namespace Spectra.Core.Text;

/// <summary>
/// Deterministic, culture-invariant text normaliser (Spec 079). Produces a <b>comparison key</b>, not display
/// text: two strings that differ only in HTML/Markdown markup, whitespace or case give the same key. Used for
/// acceptance-criterion matching and "modified" detection; later phases reuse it for section hashing.
/// </summary>
public static partial class TextNormalizer
{
    /// <summary>Returns the comparison key for <paramref name="text"/> (empty for null or blank input).</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var s = text;

        // Autolinks first (they look like tags): <https://x.y> → https://x.y
        s = AutolinkRegex().Replace(s, "$1");
        // Markdown images and links keep their visible text.
        s = ImageRegex().Replace(s, "$1");
        s = InlineLinkRegex().Replace(s, "$1");
        s = ReferenceLinkRegex().Replace(s, "$1");
        // HTML comments and tags, then entities (decoding after stripping keeps an escaped "&lt;b&gt;" as text).
        // Spec 080 B9: only well-formed tags of known HTML elements count, so comparisons such as
        // "total<limit and qty>5" and placeholders such as "<Limit>" are text, not markup.
        s = HtmlCommentRegex().Replace(s, " ");
        s = HtmlTagRegex().Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        // Inline code and emphasis. Markers only count outside words, so snake_case and "2 * 3" survive.
        s = InlineCodeRegex().Replace(s, "$1");
        s = StrongStarRegex().Replace(s, "$1");
        s = StrongUnderscoreRegex().Replace(s, "$1");
        s = StrikeRegex().Replace(s, "$1");
        s = EmStarRegex().Replace(s, "$1");
        s = EmUnderscoreRegex().Replace(s, "$1");

        s = WhitespaceRegex().Replace(s, " ").Trim();
        return s.ToLowerInvariant();
    }

    [GeneratedRegex(@"<((?:https?|mailto):[^>\s]+)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutolinkRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineLinkRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\[[^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceLinkRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentRegex();

    // Known HTML elements (lowercase) and the bare boolean attributes a tag may carry (Spec 080 B9, Spec 081 C3).
    private const string ElementNames =
        "a|abbr|article|aside|audio|b|bdi|bdo|big|blockquote|br|caption|center|cite|code|col|colgroup|dd|del"
        + "|details|dfn|div|dl|dt|em|figcaption|figure|font|h[1-6]|hr|i|iframe|img|input|ins|kbd|li|mark|ol|p|picture"
        + "|pre|q|rp|rt|ruby|s|samp|section|small|source|span|strike|strong|sub|summary|sup|table|tbody|td|tfoot|th"
        + "|thead|tr|tt|u|ul|var|video|wbr";

    private const string Attribute =
        @"\s+(?:[A-Za-z_:][-A-Za-z0-9_:.]*\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'=<>`]+)"
        + "|(?:allowfullscreen|async|autofocus|autoplay|checked|compact|controls|default|defer|disabled|hidden|ismap"
        + "|loop|multiple|muted|nohref|noresize|noshade|novalidate|nowrap|open|readonly|required|reversed|selected)"
        + @"(?=[\s/>]))";

    /// <summary>
    /// A tag of a known HTML element written in HTML form (Spec 082 A2) — one of:
    /// (a) a closing tag, any case (<c>&lt;/Summary&gt;</c>);
    /// (b) a tag with at least one attribute, valued or a bare boolean one, any case (<c>&lt;Details open&gt;</c>);
    /// (c) a bare tag with an all-lowercase name (<c>&lt;details&gt;</c>, <c>&lt;br/&gt;</c>);
    /// (d) a bare opening tag, any case, closed later in the text (<c>&lt;P&gt;Para&lt;/P&gt;</c>).
    /// Anything else in angle brackets is text: comparisons (<c>total&lt;limit and qty&gt;5</c>), placeholders
    /// (<c>&lt;Summary&gt;</c>, <c>&lt;Source&gt;</c>, <c>&lt;Limit&gt;</c>, <c>&lt;LIMIT&gt;</c>, <c>&lt;BR&gt;</c>).
    /// </summary>
    [GeneratedRegex(
        "</(?:" + ElementNames + @")\s*>"
        + "|<(?:" + ElementNames + ")(?:" + Attribute + @")+\s*/?>"
        + "|<(?-i:" + ElementNames + @")\s*/?>"
        + "|<(?<n>" + ElementNames + @")\s*>(?=[\s\S]*?</\k<n>\s*>)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"`+([^`]+?)`+", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"(?<![\w*])\*\*(?=\S)(.+?)(?<=\S)\*\*(?![\w*])", RegexOptions.CultureInvariant)]
    private static partial Regex StrongStarRegex();

    [GeneratedRegex(@"(?<![\w_])__(?=\S)(.+?)(?<=\S)__(?![\w_])", RegexOptions.CultureInvariant)]
    private static partial Regex StrongUnderscoreRegex();

    [GeneratedRegex(@"(?<![\w~])~~(?=\S)(.+?)(?<=\S)~~(?![\w~])", RegexOptions.CultureInvariant)]
    private static partial Regex StrikeRegex();

    [GeneratedRegex(@"(?<![\w*])\*(?=\S)(.+?)(?<=\S)\*(?![\w*])", RegexOptions.CultureInvariant)]
    private static partial Regex EmStarRegex();

    [GeneratedRegex(@"(?<![\w_])_(?=\S)(.+?)(?<=\S)_(?![\w_])", RegexOptions.CultureInvariant)]
    private static partial Regex EmUnderscoreRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
