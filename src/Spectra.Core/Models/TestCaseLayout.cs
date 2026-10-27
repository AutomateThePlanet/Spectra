namespace Spectra.Core.Models;

/// <summary>
/// Source layout of a parsed test file. Produced by <c>TestCaseParser</c>, consumed by the test file
/// writer so a rewrite reproduces every value it did not change byte for byte, and keeps frontmatter
/// keys and body sections the typed model does not capture.
/// </summary>
/// <param name="Baseline">The values the parser produced (with <c>Layout = null</c>). The writer compares
/// the test being written against it to decide between raw and canonical output.</param>
/// <param name="FrontmatterEntries">Every top-level frontmatter entry, in original order.</param>
/// <param name="BodySegments">Every body segment, in original order. Concatenated, the raw texts
/// reproduce the body exactly.</param>
/// <param name="FrontmatterLeading">Text before the first key: a header comment and blank lines. Always written
/// first (Spec 080 A4).</param>
/// <param name="FrontmatterTrailing">Blank lines and column-0 comments after the last key's value. Always written
/// last.</param>
/// <remarks>
/// Invariant: <c>FrontmatterLeading + Σ(entry.Prelude + entry.RawText) + FrontmatterTrailing</c> is the original
/// frontmatter, byte for byte.
/// </remarks>
/// <param name="Newline">The newline sequence of the original file (<c>"\r\n"</c> or <c>"\n"</c>).</param>
public sealed record TestCaseLayout(
    TestCase Baseline,
    IReadOnlyList<FrontmatterEntry> FrontmatterEntries,
    IReadOnlyList<BodySegment> BodySegments,
    string Newline,
    string FrontmatterLeading = "",
    string FrontmatterTrailing = "");

/// <summary>
/// One top-level frontmatter entry. <see cref="Prelude"/> is the blank lines and column-0 comment lines directly
/// above the key, which belong to it (Spec 080 A4): they move with the key and are dropped with it.
/// <see cref="RawText"/> is the exact source text of the key and its value, including the final line break.
/// </summary>
public sealed record FrontmatterEntry(string Key, string Prelude, string RawText, bool IsKnown);

/// <summary>
/// One body segment: the header line (for sections) plus its content, up to the next <c>##</c> header.
/// </summary>
public sealed record BodySegment(BodySegmentKind Kind, string RawText);

/// <summary>
/// Kind of a body segment. Only the first section of each known kind is classified as known; later
/// duplicates are <see cref="Unknown"/>, matching the parser, which reads only the first.
/// </summary>
public enum BodySegmentKind
{
    /// <summary>Text before the H1 title.</summary>
    Leading,
    /// <summary>The H1 title line.</summary>
    Title,
    /// <summary>Text between the H1 and the first <c>##</c> section.</summary>
    Intro,
    Preconditions,
    Steps,
    ExpectedResult,
    TestData,
    /// <summary>A <c>##</c> section the parser does not read.</summary>
    Unknown
}
