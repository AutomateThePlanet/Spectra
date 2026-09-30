using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

/// <summary>
/// Spec 078: the parser captures a source layout whose raw pieces reproduce the file exactly.
/// </summary>
public class TestCaseLayoutTests
{
    private const string Frontmatter =
        "# leading comment\n" +
        "id: TC-1\n" +
        "priority: high\n" +
        "owner: qa-team\n" +
        "tags: [a, b]\n" +
        "x_ticket: {id: 42, system: jira}\n" +
        "custom:\n" +
        "  notes: |\n" +
        "    first\n" +
        "    second\n" +
        "  list:\n" +
        "    - one\n" +
        "# comment between keys\n" +
        "criteria: []\n";

    private const string Body =
        "\n" +
        "# Title here\n" +
        "\n" +
        "Intro text.\n" +
        "\n" +
        "## Pre-conditions\n" +
        "\n" +
        "Logged in.\n" +
        "\n" +
        "## Test Steps\n" +
        "\n" +
        "1. a\n" +
        "   - sub\n" +
        "\n" +
        "## Notes\n" +
        "\n" +
        "n\n" +
        "\n" +
        "## Steps\n" +
        "\n" +
        "1. duplicate section\n" +
        "\n" +
        "## Expected Result\n" +
        "\n" +
        "ok\n";

    private static readonly string Markdown = "---\n" + Frontmatter + "---\n" + Body;

    // Spec 080 A4 invariant: leading + every entry's prelude and text + trailing is the frontmatter.
    private static string Reassemble(TestCaseLayout layout) =>
        layout.FrontmatterLeading +
        string.Concat(layout.FrontmatterEntries.Select(e => e.Prelude + e.RawText)) +
        layout.FrontmatterTrailing;

    private static TestCase Parse(string markdown)
    {
        var result = new TestCaseParser().Parse(markdown, "TC-1.md");
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Value.Layout);
        return result.Value;
    }

    [Fact]
    public void FrontmatterEntries_ConcatenateToTheOriginalFrontmatter()
    {
        var layout = Parse(Markdown).Layout!;

        Assert.Equal(Frontmatter, Reassemble(layout));
    }

    [Fact]
    public void BodySegments_ConcatenateToTheOriginalBody()
    {
        var layout = Parse(Markdown).Layout!;

        Assert.Equal(Body, string.Concat(layout.BodySegments.Select(s => s.RawText)));
    }

    [Fact]
    public void UnknownKeys_AreCapturedInOriginalOrder()
    {
        var unknown = Parse(Markdown).Layout!.FrontmatterEntries.Where(e => !e.IsKnown).Select(e => e.Key);

        Assert.Equal(["owner", "x_ticket"], unknown);
    }

    [Fact]
    public void Entries_CaptureFlowCollectionsBlockScalarsAndComments()
    {
        var entries = Parse(Markdown).Layout!.FrontmatterEntries.ToDictionary(e => e.Key);

        var layout = Parse(Markdown).Layout!;

        // Spec 080 A4: the header comment is the frontmatter's leading text, not part of the first key; a comment
        // between keys belongs to the key below it; RawText is the key and its value only.
        Assert.Equal("# leading comment\n", layout.FrontmatterLeading);
        Assert.Equal("id: TC-1\n", entries["id"].RawText);
        Assert.Equal("", entries["id"].Prelude);
        Assert.Equal("tags: [a, b]\n", entries["tags"].RawText);
        Assert.Equal("x_ticket: {id: 42, system: jira}\n", entries["x_ticket"].RawText);
        Assert.Contains("    first\n    second\n", entries["custom"].RawText);
        Assert.DoesNotContain("# comment between keys", entries["custom"].RawText);
        Assert.Equal("# comment between keys\n", entries["criteria"].Prelude);
        Assert.Equal("criteria: []\n", entries["criteria"].RawText);
        Assert.Equal("", layout.FrontmatterTrailing);
        Assert.Equal(Frontmatter, Reassemble(layout));
    }

    [Fact]
    public void LiteralBlockWithDashLine_IsKeptInFrontmatter()
    {
        const string md = "---\nid: TC-1\npriority: low\ncustom:\n  notes: |\n    a\n    ---\n    b\n---\n\n# T\n";

        // An indented "---" inside a block scalar is content, not a delimiter; it must survive.
        var t = Parse(md);

        Assert.Contains("    ---\n    b\n", t.Layout!.FrontmatterEntries.Single(e => e.Key == "custom").RawText);
        Assert.Equal("a\n---\nb\n", t.Custom!["notes"]);
    }

    [Fact]
    public void Sections_AreClassified_AndDuplicatesAreUnknown()
    {
        var kinds = Parse(Markdown).Layout!.BodySegments.Select(s => s.Kind);

        Assert.Equal(
            [
                BodySegmentKind.Leading, BodySegmentKind.Title, BodySegmentKind.Intro,
                BodySegmentKind.Preconditions, BodySegmentKind.Steps, BodySegmentKind.Unknown,
                BodySegmentKind.Unknown, BodySegmentKind.ExpectedResult
            ],
            kinds);
    }

    [Theory]
    [InlineData("\n", "\n")]
    [InlineData("\r\n", "\r\n")]
    public void Newline_IsDetected(string newline, string expected)
    {
        var md = Markdown.Replace("\n", newline);

        Assert.Equal(expected, Parse(md).Layout!.Newline);
    }

    [Fact]
    public void Crlf_RawPiecesStillReproduceTheFile()
    {
        var md = Markdown.Replace("\n", "\r\n");
        var layout = Parse(md).Layout!;

        Assert.Equal(Frontmatter.Replace("\n", "\r\n"), Reassemble(layout));
        Assert.Equal(Body.Replace("\n", "\r\n"), string.Concat(layout.BodySegments.Select(s => s.RawText)));
    }

    [Fact]
    public void Baseline_HasNoLayout_AndMatchesTheParsedValues()
    {
        var t = Parse(Markdown);
        var baseline = t.Layout!.Baseline;

        Assert.Null(baseline.Layout);
        Assert.Equal(t with { Layout = null }, baseline);
        Assert.Equal("Logged in.", baseline.Preconditions);
        Assert.Equal(["a"], baseline.Steps);
    }
}
