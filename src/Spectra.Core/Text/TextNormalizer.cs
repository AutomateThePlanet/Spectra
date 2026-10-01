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
        var source = s;
        s = HtmlTagRegex().Replace(s, m => IsMarkup(m, source) ? " " : m.Value);
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
    /// Candidate tags of a known HTML element (Spec 083 F4). The alternatives, in order:
    /// a closing tag <c>&lt;/name&gt;</c> (any case); a tag with at least one well-formed attribute — valued or a known
    /// bare boolean one — (any case); a self-closing bare tag <c>&lt;name/&gt;</c> (any case); the void elements
    /// <c>&lt;br&gt;</c>, <c>&lt;hr&gt;</c>, <c>&lt;wbr&gt;</c> in lowercase; and a bare opening tag, captured as
    /// <c>bare</c>, which <see cref="IsMarkup"/> accepts only when a closing tag of exactly the same spelling follows.
    /// </summary>
    [GeneratedRegex(
        "</(?:" + ElementNames + @")\s*>"
        + "|<(?:" + ElementNames + ")(?:" + Attribute + @")+\s*/?>"
        + "|<(?:" + ElementNames + @")\s*/>"
        + @"|<(?-i:br|hr|wbr)\s*>"
        + "|<(?<bare>" + ElementNames + @")\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    /// Spec 083 F4: whether a candidate tag is markup. Every form except a bare opening tag is; a bare opening tag is
    /// markup only when <c>&lt;/</c> + its exact spelling (case-sensitive) + <c>&gt;</c> appears later in the same text —
    /// <c>&lt;p&gt;x&lt;/p&gt;</c>, <c>&lt;P&gt;x&lt;/P&gt;</c>. Anything else stays text, because a hidden wording change
    /// is worse than a spurious "modified": <c>&lt;source&gt;</c> or <c>&lt;summary&gt;</c> with no matching close,
    /// <c>&lt;Summary&gt;</c>, <c>&lt;LIMIT&gt;</c>, <c>&lt;BR&gt;</c>, and <c>&lt;Summary&gt;</c> when only
    /// <c>&lt;/summary&gt;</c> follows. Accepted residual: a lowercase placeholder whose exact lowercase closing tag also
    /// appears later in the same text is treated as markup.
    /// </summary>
    private static bool IsMarkup(Match match, string text)
    {
        var bare = match.Groups["bare"];
        if (!bare.Success)
        {
            return true;
        }

        var after = text[(match.Index + match.Length)..];
        return Regex.IsMatch(after, "</" + Regex.Escape(bare.Value) + @"\s*>", RegexOptions.CultureInvariant);
    }

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
