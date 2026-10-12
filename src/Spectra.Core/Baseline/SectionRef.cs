using System.Text;
using System.Text.RegularExpressions;

namespace Spectra.Core.Baseline;

/// <summary>
/// Spec 094 (R1): the canonical ref of a section — the repo-relative path for a whole document, else <c>path#anchor</c>,
/// where the anchor is the heading's first explicit id or a GitHub-style slug of its text (<c>-n</c> for the n-th
/// duplicate). A canonical anchor always resolves back to the same heading through <see cref="SectionExtractor.Match"/>;
/// when no candidate does (a heading without letters or digits), there is no canonical ref.
/// </summary>
public static partial class SectionRef
{
    /// <summary>
    /// The canonical ref of heading <paramref name="index"/> of <paramref name="relativePath"/> (null index = the whole
    /// document), or null when no anchor resolves back to that heading.
    /// </summary>
    public static string? Canonical(string relativePath, IReadOnlyList<MarkdownHeading> headings, int? index)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(headings);
        if (index is null)
        {
            return relativePath;
        }

        var i = index.Value;
        foreach (var anchor in Candidates(headings, i))
        {
            if (anchor.Length > 0 && SectionExtractor.Match(headings, anchor)?.Index == i)
            {
                return $"{relativePath}#{anchor}";
            }
        }

        return null;
    }

    /// <summary>A GitHub-style slug: lowercase, whitespace → <c>-</c>, only letters, digits, <c>-</c> and <c>_</c> kept.</summary>
    public static string Slug(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                builder.Append('-');
            }
            else if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Candidates(IReadOnlyList<MarkdownHeading> headings, int i)
    {
        var heading = headings[i];
        var explicitId = ExplicitIdRegex().Match(heading.Text);
        if (explicitId.Success)
        {
            yield return explicitId.Groups["id"].Value;
        }

        var slug = Slug(heading.CleanText);
        var earlier = Enumerable.Range(0, i).Count(j => headings[j].TextKey == heading.TextKey);
        yield return earlier == 0 ? slug : $"{slug}-{earlier}";
    }

    [GeneratedRegex(@"(?:\{[^}]*#(?<id>[^\s}]+)[^}]*\}\s*$)|(?:\bid\s*=\s*[""'](?<id>[^""']+)[""'])")]
    private static partial Regex ExplicitIdRegex();
}
