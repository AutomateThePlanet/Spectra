using System.Text.RegularExpressions;

namespace Spectra.Core.Baseline;

/// <summary>An ATX heading outside fenced code: its level (1–6), text (closing hashes stripped) and 0-based line.</summary>
public sealed record MarkdownHeading(int Level, string Text, int StartLine);

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
        var matches = key.Length == 0
            ? []
            : headings.Select((h, index) => (h, index)).Where(x => AnchorKey.Make(x.h.Text) == key).ToList();
        if (matches.Count == 0)
        {
            return new SectionLookup(SectionOutcome.AnchorNotFound, null, false, key);
        }

        var (first, position) = matches[0];
        var endLine = headings.Skip(position + 1).FirstOrDefault(h => h.Level <= first.Level)?.StartLine;
        var lineStarts = LineStarts(normalised);
        var start = lineStarts[first.StartLine];
        var end = endLine is { } e ? lineStarts[e] : normalised.Length;
        return new SectionLookup(SectionOutcome.Found, normalised[start..end], matches.Count > 1, key);
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

        var key = AnchorKey.FromAnchor(anchor);
        var first = key.Length == 0 ? -1 : headings.ToList().FindIndex(h => AnchorKey.Make(h.Text) == key);
        if (first < 0)
        {
            return [];
        }

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
}
