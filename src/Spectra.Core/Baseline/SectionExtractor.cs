using System.Text.RegularExpressions;
using Spectra.Core.Text;

namespace Spectra.Core.Baseline;

/// <summary>
/// An ATX heading outside fenced code: its level (1–6), raw text (closing hashes stripped) and 0-based line. Spec 085
/// adds the match data, computed from <see cref="Text"/>: <see cref="CleanText"/> (markup, links and a trailing
/// attribute block removed, case kept), <see cref="TextKey"/> and <see cref="ExplicitIds"/>.
/// </summary>
public sealed record MarkdownHeading(int Level, string Text, int StartLine)
{
    /// <summary>The heading text without a trailing <c>{#…}</c> / <c>{.…}</c> block, HTML or links, in its own case.</summary>
    public string CleanText { get; } = TextNormalizer.Clean(SectionExtractor.StripAttributeBlock(Text));

    /// <summary>The anchor key of <see cref="CleanText"/> (rule 1).</summary>
    public string TextKey => AnchorKey.Make(CleanText);

    /// <summary>The anchor keys of every <c>id="…"</c> / <c>id='…'</c> in the heading and of a trailing <c>{#id}</c> (rule 2).</summary>
    public IReadOnlyList<string> ExplicitIds => SectionExtractor.ExplicitIdKeys(Text);
}

/// <summary>Which rule matched an anchor to a heading (Spec 085 FR-003), in precedence order.</summary>
public enum MatchRule
{
    Text,
    ExplicitId,
    DuplicateSuffix
}

/// <summary>The heading an anchor resolved to (<see cref="Index"/> into the heading list) and how.</summary>
public sealed record HeadingMatch(int Index, MatchRule Rule, bool Ambiguous);

public enum SectionOutcome
{
    /// <summary>The ref has no anchor (or an empty one): the section is the whole file.</summary>
    WholeFile,

    /// <summary>A heading's key matched the anchor's key.</summary>
    Found,

    /// <summary>No heading's key matched the anchor's key.</summary>
    AnchorNotFound
}

/// <summary>The section a ref identifies. <see cref="Ambiguous"/>: more than one heading shares the anchor's key.</summary>
public sealed record SectionLookup(SectionOutcome Outcome, string? RawText, bool Ambiguous, string AnchorKey);

/// <summary>
/// Spec 084 (D1, FR-001–FR-003): the unit of change. A ref <c>file#anchor</c> identifies the first ATX heading whose
/// key equals the anchor's key, through to just before the next heading of the same or a higher level (or the end of
/// the file); the heading line is included. A ref without an anchor identifies the whole file. Headings inside fenced
/// code (<c>```</c> or <c>~~~</c>) are not headings. Setext headings are not recognised (a documented residual).
/// Works on text with line endings normalised to LF and a leading BOM removed.
/// </summary>
public static partial class SectionExtractor
{
    /// <summary>Removes a leading BOM and normalises CRLF and lone CR to LF.</summary>
    public static string NormalizeNewlines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>The anchor of a ref: the text after the first <c>#</c>, or null when there is none.</summary>
    public static string? AnchorOf(string sourceRef)
    {
        ArgumentNullException.ThrowIfNull(sourceRef);
        var hash = sourceRef.IndexOf('#');
        return hash < 0 ? null : sourceRef[(hash + 1)..];
    }

    /// <summary>The ATX headings of <paramref name="text"/> outside fenced code, in document order.</summary>
    public static IReadOnlyList<MarkdownHeading> Headings(string text)
    {
        var lines = NormalizeNewlines(text).Split('\n');
        var headings = new List<MarkdownHeading>();
        char fenceChar = '\0';
        var fenceLength = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (fenceChar != '\0')
            {
                var close = FenceRegex().Match(line);
                if (close.Success && close.Groups["rest"].Value.Trim().Length == 0
                    && close.Groups["fence"].Value[0] == fenceChar && close.Groups["fence"].Length >= fenceLength)
                {
                    fenceChar = '\0';
                }

                continue;
            }

            var open = FenceRegex().Match(line);
            if (open.Success)
            {
                fenceChar = open.Groups["fence"].Value[0];
                fenceLength = open.Groups["fence"].Length;
                continue;
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                headings.Add(new MarkdownHeading(heading.Groups["hashes"].Length, heading.Groups["text"].Value.Trim(), i));
            }
        }

        return headings;
    }

    /// <summary>The section <paramref name="anchor"/> identifies in <paramref name="text"/>.</summary>
    public static SectionLookup Extract(string text, string? anchor)
    {
        var normalised = NormalizeNewlines(text);
        if (string.IsNullOrEmpty(anchor))
        {
            return new SectionLookup(SectionOutcome.WholeFile, normalised, false, string.Empty);
        }

        var key = AnchorKey.FromAnchor(anchor);
        var headings = Headings(normalised);
        var match = Match(headings, anchor);
        if (match is null)
        {
            return new SectionLookup(SectionOutcome.AnchorNotFound, null, false, key);
        }

        var first = headings[match.Index];
        var endLine = headings.Skip(match.Index + 1).FirstOrDefault(h => h.Level <= first.Level)?.StartLine;
        var lineStarts = LineStarts(normalised);
        var start = lineStarts[first.StartLine];
        var end = endLine is { } e ? lineStarts[e] : normalised.Length;
        return new SectionLookup(SectionOutcome.Found, normalised[start..end], match.Ambiguous, key);
    }

    /// <summary>
    /// Spec 085 FR-003: the heading <paramref name="anchor"/> resolves to, by the first rule that matches —
    /// (1) the first heading whose <see cref="MarkdownHeading.TextKey"/> equals the anchor's key (ambiguous when several
    /// do); (2) the first heading with that key among its <see cref="MarkdownHeading.ExplicitIds"/>; (3) a GitHub
    /// duplicate suffix: an anchor <c>base-n</c> (n ≥ 1) with at least n+1 headings whose text key is the base's gives
    /// the (n+1)th. Null when nothing matches. Extraction and coverage both use this, so they always agree.
    /// </summary>
    public static HeadingMatch? Match(IReadOnlyList<MarkdownHeading> headings, string? anchor)
    {
        ArgumentNullException.ThrowIfNull(headings);
        if (string.IsNullOrEmpty(anchor))
        {
            return null;
        }

        var key = AnchorKey.FromAnchor(anchor);
        if (key.Length == 0)
        {
            return null;
        }

        var byText = Enumerable.Range(0, headings.Count).Where(i => headings[i].TextKey == key).ToList();
        if (byText.Count > 0)
        {
            return new HeadingMatch(byText[0], MatchRule.Text, byText.Count > 1);
        }

        for (var i = 0; i < headings.Count; i++)
        {
            if (headings[i].ExplicitIds.Contains(key))
            {
                return new HeadingMatch(i, MatchRule.ExplicitId, false);
            }
        }

        var suffix = DuplicateSuffixRegex().Match(Decode(anchor));
        if (suffix.Success && int.TryParse(suffix.Groups["n"].Value, out var n))
        {
            var baseKey = AnchorKey.Make(suffix.Groups["base"].Value);
            var same = Enumerable.Range(0, headings.Count).Where(i => headings[i].TextKey == baseKey).ToList();
            if (baseKey.Length > 0 && same.Count >= n + 1)
            {
                return new HeadingMatch(same[n], MatchRule.DuplicateSuffix, false);
            }
        }

        return null;
    }

    /// <summary>
    /// Spec 085 FR-001a: a section's comparison text — <see cref="TextNormalizer.Normalize"/> after removing the trailing
    /// <c>{#…}</c> / <c>{.…}</c> attribute block from every ATX heading line outside fenced code, so adding or changing a
    /// custom ID is never a doc change. (Anchor tags and links are already removed by the normaliser.)
    /// </summary>
    public static string ComparisonText(string rawSection)
    {
        ArgumentNullException.ThrowIfNull(rawSection);
        var lines = NormalizeNewlines(rawSection).Split('\n');
        char fenceChar = '\0';
        var fenceLength = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (fenceChar != '\0')
            {
                var close = FenceRegex().Match(line);
                if (close.Success && close.Groups["rest"].Value.Trim().Length == 0
                    && close.Groups["fence"].Value[0] == fenceChar && close.Groups["fence"].Length >= fenceLength)
                {
                    fenceChar = '\0';
                }

                continue;
            }

            var open = FenceRegex().Match(line);
            if (open.Success)
            {
                fenceChar = open.Groups["fence"].Value[0];
                fenceLength = open.Groups["fence"].Length;
                continue;
            }

            if (HeadingRegex().IsMatch(line))
            {
                lines[i] = StripAttributeBlock(line);
            }
        }

        return TextNormalizer.Normalize(string.Join('\n', lines));
    }

    /// <summary>Removes a trailing Pandoc/kramdown attribute block (<c>{#id}</c>, <c>{: #id .cls}</c>, <c>{.cls}</c>).</summary>
    public static string StripAttributeBlock(string text) => AttributeBlockRegex().Replace(text, string.Empty);

    /// <summary>The anchor keys of a heading's explicit IDs: <c>id="…"</c> / <c>id='…'</c> attributes and a trailing <c>{#id}</c>.</summary>
    public static IReadOnlyList<string> ExplicitIdKeys(string headingText)
    {
        var ids = new List<string>();
        foreach (Match m in IdAttributeRegex().Matches(headingText))
        {
            ids.Add(AnchorKey.Make(m.Groups["dq"].Success ? m.Groups["dq"].Value : m.Groups["sq"].Value));
        }

        var block = AttributeBlockRegex().Match(headingText);
        if (block.Success && BlockIdRegex().Match(block.Groups["attrs"].Value) is { Success: true } id)
        {
            ids.Add(AnchorKey.Make(id.Groups["id"].Value));
        }

        return ids.Where(k => k.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    private static string Decode(string anchor)
    {
        try
        {
            return Uri.UnescapeDataString(anchor);
        }
        catch (UriFormatException)
        {
            return anchor;
        }
    }

    /// <summary>
    /// The indexes (into <paramref name="headings"/>) of the headings a ref with <paramref name="anchor"/> covers: all
    /// of them without an anchor; otherwise the matched heading and every heading nested in its section; none when the
    /// anchor matches nothing.
    /// </summary>
    public static IReadOnlyList<int> CoveredHeadings(IReadOnlyList<MarkdownHeading> headings, string? anchor)
    {
        if (string.IsNullOrEmpty(anchor))
        {
            return Enumerable.Range(0, headings.Count).ToList();
        }

        var match = Match(headings, anchor);
        if (match is null)
        {
            return [];
        }

        var first = match.Index;

        var covered = new List<int> { first };
        for (var i = first + 1; i < headings.Count && headings[i].Level > headings[first].Level; i++)
        {
            covered.Add(i);
        }

        return covered;
    }

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    // Up to three leading spaces, then three or more backticks or tildes.
    [GeneratedRegex(@"^ {0,3}(?<fence>`{3,}|~{3,})(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FenceRegex();

    // Up to three leading spaces, 1–6 hashes, then a space/tab and the text (or nothing); closing hashes after a space
    // are stripped. "#NoSpace" is not a heading.
    [GeneratedRegex(@"^ {0,3}(?<hashes>#{1,6})(?:[ \t]+(?<text>.*?))?(?:[ \t]+#+)?[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    // Spec 085: a trailing attribute block beginning with # or . (optionally after a kramdown colon).
    [GeneratedRegex(@"\s*\{:?\s*(?<attrs>[#.][^}]*)\}\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeBlockRegex();

    [GeneratedRegex(@"#(?<id>[^\s}.#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex BlockIdRegex();

    [GeneratedRegex(@"\bid\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdAttributeRegex();

    // A GitHub duplicate anchor: base-n with n >= 1.
    [GeneratedRegex(@"^(?<base>.+)-(?<n>[1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DuplicateSuffixRegex();
}
