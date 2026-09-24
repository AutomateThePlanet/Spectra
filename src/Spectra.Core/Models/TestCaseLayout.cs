namespace Spectra.Core.Models;

/// <summary>
/// Source layout of a parsed test file. Produced by <c>TestCaseParser</c>, consumed by the test file
/// writer so a rewrite reproduces every value it did not change byte for byte, and keeps frontmatter
/// keys and body sections the typed model does not capture.
/// </summary>
/// <param name="Baseline">The values the parser produced (with <c>Layout = null</c>). The writer compares
/// the test being written against it to decide between raw and canonical output.</param>
/// <param name="FrontmatterEntries">Every top-level frontmatter entry, in original order. Concatenated,
/// the raw texts reproduce the frontmatter exactly.</param>
/// <param name="BodySegments">Every body segment, in original order. Concatenated, the raw texts
/// reproduce the body exactly.</param>
/// <param name="Newline">The newline sequence of the original file (<c>"\r\n"</c> or <c>"\n"</c>).</param>
public sealed record TestCaseLayout(
    TestCase Baseline,
    IReadOnlyList<FrontmatterEntry> FrontmatterEntries,
    IReadOnlyList<BodySegment> BodySegments,
    string Newline);

/// <summary>
/// One top-level frontmatter entry: the key and the exact source text from the key up to the next
/// top-level key (including trailing comments and the final line break).
/// </summary>
public sealed record FrontmatterEntry(string Key, string RawText, bool IsKnown);

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
