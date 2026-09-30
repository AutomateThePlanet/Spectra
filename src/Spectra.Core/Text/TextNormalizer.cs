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

    /// <summary>
    /// An opening, closing or self-closing tag of a known inline/block HTML element, with well-formed attributes
    /// (<c>name="v"</c>, <c>name='v'</c> or <c>name=v</c>). Anything else in angle brackets is kept as text.
    /// </summary>
    [GeneratedRegex(
        @"</?(?:a|abbr|b|blockquote|br|cite|code|dd|del|div|dl|dt|em|h[1-6]|hr|i|img|ins|kbd|li|mark|ol|p|pre|q|s|small|span|strike|strong|sub|sup|table|tbody|td|th|thead|tr|u|ul|var)"
        + @"(?:\s+[A-Za-z_:][-A-Za-z0-9_:.]*\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'=<>`]+))*\s*/?>",
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
