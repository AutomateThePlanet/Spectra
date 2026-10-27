using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>Spec 084 T1 (FR-001, FR-002): which text a ref's section is. One row per rule.</summary>
public class SectionExtractorTests
{
    private const string Levels =
        "# Title\n\nIntro\n\n## A\n\na text\n\n### A1\n\na1 text\n\n#### A1x\n\ndeep\n\n## B\n\nb text\n\n# Next\n\nnext text\n";

    public static TheoryData<string, string?, SectionOutcome, string?, bool> Rows => new()
    {
        // Level 2 includes its level 3 and 4 children and ends at the next level 2.
        { Levels, "A", SectionOutcome.Found, "## A\n\na text\n\n### A1\n\na1 text\n\n#### A1x\n\ndeep\n\n", false },
        // Level 3 ends at the next level 2 (a higher level).
        { Levels, "A1", SectionOutcome.Found, "### A1\n\na1 text\n\n#### A1x\n\ndeep\n\n", false },
        // Level 4 runs to the next heading of level ≤ 4.
        { Levels, "A1x", SectionOutcome.Found, "#### A1x\n\ndeep\n\n", false },
        // Level 2 ends at the next level 1.
        { Levels, "B", SectionOutcome.Found, "## B\n\nb text\n\n", false },
        // Level 1 runs to the next level 1.
        { Levels, "Title", SectionOutcome.Found, "# Title\n\nIntro\n\n## A\n\na text\n\n### A1\n\na1 text\n\n#### A1x\n\ndeep\n\n## B\n\nb text\n\n", false },
        // The last section runs to the end of the file.
        { Levels, "Next", SectionOutcome.Found, "# Next\n\nnext text\n", false },
        // Levels 5 and 6.
        { "##### Five\nv\n###### Six\nvi\n##### Five2\n", "Five", SectionOutcome.Found, "##### Five\nv\n###### Six\nvi\n", false },
        { "##### Five\nv\n###### Six\nvi\n##### Five2\n", "Six", SectionOutcome.Found, "###### Six\nvi\n", false },
        // A heading inside a ``` fence is neither a boundary nor a target.
        { "## A\n```\n## Fake\n```\nafter\n## B\n", "A", SectionOutcome.Found, "## A\n```\n## Fake\n```\nafter\n", false },
        { "## A\n```\n## Fake\n```\n", "Fake", SectionOutcome.AnchorNotFound, null, false },
        // Same for a ~~~ fence.
        { "## A\n~~~\n# Fake\n~~~\nx\n", "A", SectionOutcome.Found, "## A\n~~~\n# Fake\n~~~\nx\n", false },
        // A 4-backtick fence is not closed by a 3-backtick line.
        { "## A\n````\n```\n## Fake\n```\n````\nx\n## B\n", "A", SectionOutcome.Found, "## A\n````\n```\n## Fake\n```\n````\nx\n", false },
        // A ~~~ line does not close a ``` fence.
        { "## A\n```\n~~~\n## Fake\n```\n## B\n", "A", SectionOutcome.Found, "## A\n```\n~~~\n## Fake\n```\n", false },
        // No anchor, or an empty one: the whole file.
        { "# A\nx\n## B\ny\n", null, SectionOutcome.WholeFile, "# A\nx\n## B\ny\n", false },
        { "# A\nx\n", "", SectionOutcome.WholeFile, "# A\nx\n", false },
        // Duplicate headings: the first wins, and the result is ambiguous.
        { "## Limits\nfirst\n## Other\no\n## Limits\nsecond\n", "Limits", SectionOutcome.Found, "## Limits\nfirst\n", true },
        // Closing hashes are stripped from the heading text.
        { "## Quotas ##\nq\n", "Quotas", SectionOutcome.Found, "## Quotas ##\nq\n", false },
        // A hash that is part of the text (no space before it) is kept.
        { "## C#\nc\n", "C", SectionOutcome.Found, "## C#\nc\n", false },
        // Up to three leading spaces still make a heading.
        { "   ## Indented\ni\n## Next\n", "Indented", SectionOutcome.Found, "   ## Indented\ni\n", false },
        // Four leading spaces do not.
        { "    ## Code\nc\n", "Code", SectionOutcome.AnchorNotFound, null, false },
        // "#NoSpace" is not a heading.
        { "#NoSpace\nx\n", "NoSpace", SectionOutcome.AnchorNotFound, null, false },
        // No heading matches.
        { "## A\nx\n", "Missing", SectionOutcome.AnchorNotFound, null, false },
        // CRLF input is extracted with LF.
        { "## A\r\nx\r\n## B\r\n", "A", SectionOutcome.Found, "## A\nx\n", false },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Extract(string text, string? anchor, SectionOutcome outcome, string? section, bool ambiguous)
    {
        var result = SectionExtractor.Extract(text, anchor);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(section, result.RawText);
        Assert.Equal(ambiguous, result.Ambiguous);
    }

    [Theory]
    [InlineData("docs/a.md#X#Y", "X#Y")]
    [InlineData("docs/a.md#", "")]
    [InlineData("docs/a.md", null)]
    public void AnchorOf_IsTheTextAfterTheFirstHash(string sourceRef, string? anchor) =>
        Assert.Equal(anchor, SectionExtractor.AnchorOf(sourceRef));

    [Fact]
    public void Headings_ListsLevelsTextAndLines()
    {
        var headings = SectionExtractor.Headings("# One\n```\n# no\n```\n## Two ##\n");

        Assert.Equal(
            [new MarkdownHeading(1, "One", 0), new MarkdownHeading(2, "Two", 4)],
            headings);
    }
}
