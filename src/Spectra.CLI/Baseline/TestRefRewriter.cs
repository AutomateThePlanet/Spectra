using System.Text;
using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Parsing;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Spectra.CLI.Baseline;

/// <summary>A rewritten test file, and a warning when the in-place edit had to fall back to the canonical writer.</summary>
public sealed record RefRewrite(string Text, string? Warning);

/// <summary>
/// Spec 086 (FR-008–FR-010, SC-003, research R6): rewrites a test's <c>source_refs</c> values in place. Only the
/// changed scalars' text changes, in their own quoting style, so untouched refs keep theirs; a de-duplicated ref loses
/// its <c>- …</c> line. Orphan clearing goes through the 078 lossless writer. The result is re-parsed and compared with
/// the expected test; on any mismatch the canonical writer is used instead, with a warning — a wrong edit never
/// persists silently.
/// </summary>
public static class TestRefRewriter
{
    private static readonly TestFileWriter Writer = new();

    /// <param name="fileText">The test file as it is on disk.</param>
    /// <param name="parsed">The test parsed from <paramref name="fileText"/> (with its layout).</param>
    /// <param name="plan">Aligned with <c>parsed.SourceRefs</c>: the value each item gets, or null to drop the item.</param>
    /// <param name="clearOrphan">Remove <c>status</c>, <c>orphaned_reason</c> and <c>orphaned_date</c> (FR-010).</param>
    /// <param name="displayPath">The file's repo-relative path, for the warning.</param>
    public static RefRewrite Rewrite(
        string fileText, TestCase parsed, IReadOnlyList<string?> plan, bool clearOrphan, string displayPath)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        if (plan.Count != parsed.SourceRefs.Count)
        {
            throw new ArgumentException("The plan must have one item per source ref.", nameof(plan));
        }

        var expectedRefs = plan.Where(r => r is not null).Select(r => r!).ToList();
        var expected = Expected(parsed, expectedRefs, clearOrphan);

        var text = EditInPlace(fileText, parsed, plan);
        if (text is not null && clearOrphan && Parse(text, parsed.FilePath) is { } edited)
        {
            text = Writer.FormatTestCase(edited with { Status = null, OrphanedReason = null, OrphanedDate = null });
        }

        if (text is not null && Parse(text, parsed.FilePath) is { } result && Canonical(result) == Canonical(expected))
        {
            return new RefRewrite(text, null);
        }

        return new RefRewrite(Writer.FormatTestCase(expected),
            $"{displayPath}: source_refs could not be edited in place; the list was rewritten canonically.");
    }

    private static TestCase Expected(TestCase parsed, IReadOnlyList<string> refs, bool clearOrphan) =>
        clearOrphan
            ? parsed with { SourceRefs = refs, Status = null, OrphanedReason = null, OrphanedDate = null }
            : parsed with { SourceRefs = refs };

    private static string Canonical(TestCase test) => Writer.FormatTestCase(test with { Layout = null });

    private static TestCase? Parse(string text, string filePath)
    {
        var result = new TestCaseParser().Parse(text, filePath);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>The scalar-level edit of the <c>source_refs</c> entry, or null when it can't be done safely.</summary>
    private static string? EditInPlace(string fileText, TestCase parsed, IReadOnlyList<string?> plan)
    {
        var layout = parsed.Layout;
        var entry = layout?.FrontmatterEntries.FirstOrDefault(e => e.Key == "source_refs");
        if (layout is null || entry is null)
        {
            return null;
        }

        // Locate the entry: the frontmatter is Leading + Σ(prelude + raw) + Trailing, byte for byte (Spec 080 A4).
        var before = new StringBuilder(layout.FrontmatterLeading);
        foreach (var e in layout.FrontmatterEntries)
        {
            if (ReferenceEquals(e, entry)) break;
            before.Append(e.Prelude).Append(e.RawText);
        }

        before.Append(entry.Prelude);
        var frontmatter = layout.FrontmatterLeading
            + string.Concat(layout.FrontmatterEntries.Select(e => e.Prelude + e.RawText)) + layout.FrontmatterTrailing;
        var frontmatterAt = fileText.IndexOf(frontmatter, StringComparison.Ordinal);
        if (frontmatterAt < 0)
        {
            return null;
        }

        var raw = entry.RawText;
        var scalars = new List<Scalar>();
        bool flow = false;
        try
        {
            var parser = new Parser(new StringReader(raw));
            var keySeen = false;
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case Scalar scalar when !keySeen:
                        keySeen = true;
                        break;
                    case Scalar scalar:
                        scalars.Add(scalar);
                        break;
                    case SequenceStart sequence:
                        flow = sequence.Style == YamlDotNet.Core.Events.SequenceStyle.Flow;
                        break;
                }
            }
        }
        catch (YamlException)
        {
            return null;
        }

        if (scalars.Count != plan.Count)
        {
            return null;
        }

        var edited = raw;
        for (var i = scalars.Count - 1; i >= 0; i--)
        {
            var scalar = scalars[i];
            var start = (int)scalar.Start.Index;
            var end = (int)scalar.End.Index;
            if (plan[i] is null)
            {
                if (flow)
                {
                    return null;
                }

                // Drop the whole "- …" line.
                var lineStart = edited.LastIndexOf('\n', Math.Max(start - 1, 0)) + 1;
                var lineEnd = edited.IndexOf('\n', end);
                edited = edited.Remove(lineStart, (lineEnd < 0 ? edited.Length : lineEnd + 1) - lineStart);
            }
            else if (!string.Equals(plan[i], scalar.Value, StringComparison.Ordinal))
            {
                var rendered = Render(plan[i]!, scalar.Style, flow);
                if (rendered is null)
                {
                    return null;
                }

                edited = edited[..start] + rendered + edited[end..];
            }
        }

        var at = frontmatterAt + before.Length;
        return fileText[..at] + edited + fileText[(at + raw.Length)..];
    }

    /// <summary>The value in the original quoting style; a plain value that would need quoting becomes double-quoted.</summary>
    private static string? Render(string value, ScalarStyle style, bool flow) => style switch
    {
        ScalarStyle.SingleQuoted => "'" + value.Replace("'", "''") + "'",
        ScalarStyle.DoubleQuoted => DoubleQuoted(value),
        ScalarStyle.Plain => IsPlainSafe(value, flow) ? value : DoubleQuoted(value),
        _ => null
    };

    private static string DoubleQuoted(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>A conservative test for a value that reads back as the same string when written plain.</summary>
    private static bool IsPlainSafe(string value, bool flow)
    {
        if (value.Length == 0 || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])
            || "-?:,[]{}#&*!|>'\"%@`".Contains(value[0]) || value.EndsWith(':'))
        {
            return false;
        }

        if (value.Contains(": ", StringComparison.Ordinal) || value.Contains(" #", StringComparison.Ordinal)
            || value.Any(char.IsControl) || (flow && value.IndexOfAny([',', '[', ']', '{', '}']) >= 0))
        {
            return false;
        }

        return value is not ("true" or "false" or "null" or "~" or "yes" or "no" or "on" or "off")
            && !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
    }
}
