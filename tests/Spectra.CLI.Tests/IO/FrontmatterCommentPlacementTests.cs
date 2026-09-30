using Spectra.CLI.IO;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 080 A4 (FR-004, FR-004a, FR-004b, SC-003): a comment or blank line directly above a frontmatter key stays
/// with that key through a rewrite; comments at the top stay at the top and comments after the last key stay at the
/// end. A removed key takes the comment above it with it.
/// </summary>
public class FrontmatterCommentPlacementTests
{
    // `priority` is written before `id` on purpose: the canonical order moves `id` first on a rewrite.
    private const string Markdown =
        "---\n" +
        "# file header comment\n" +
        "\n" +
        "priority: high\n" +
        "id: TC-200\n" +
        "component: checkout\n" +
        "# deps\n" +
        "depends_on: TC-001\n" +
        "\n" +
        "# refs below\n" +
        "source_refs:\n" +
        "  - docs/checkout.md\n" +
        "status: active\n" +
        "# custom note\n" +
        "x_custom: keep\n" +
        "# end\n" +
        "---\n" +
        "\n" +
        "# Comment placement\n" +
        "\n" +
        "## Steps\n" +
        "\n" +
        "1. Do it\n" +
        "\n" +
        "## Expected Result\n" +
        "\n" +
        "Done\n";

    private static TestCase Parse(string md) => FullFieldTestCase.Parse(md);

    private static string Write(TestCase t) => new TestFileWriter().FormatTestCase(t);

    private static string[] FrontmatterLines(string md)
    {
        var lines = md.Split('\n');
        var close = Array.IndexOf(lines, "---", 1);
        return lines[1..close];
    }

    private static string LineAbove(string[] lines, string startsWith)
    {
        var i = Array.FindIndex(lines, l => l.StartsWith(startsWith, StringComparison.Ordinal));
        Assert.True(i > 0, $"'{startsWith}' not found or first");
        return lines[i - 1];
    }

    [Fact]
    public void CommentAboveAKey_StaysWhenThePreviousKeyChanges()
    {
        var t = Parse(Markdown);

        var lines = FrontmatterLines(Write(t with { DependsOn = "TC-002" }));

        Assert.Equal("# deps", LineAbove(lines, "depends_on:"));
        Assert.Equal("# refs below", LineAbove(lines, "source_refs:"));
        var refs = Array.IndexOf(lines, "# refs below");
        Assert.Equal("", lines[refs - 1]);
        Assert.Contains("depends_on: TC-002", lines);
    }

    [Fact]
    public void LeadingComment_StaysFirst_WhenKeysAreReordered()
    {
        var t = Parse(Markdown);

        var lines = FrontmatterLines(Write(t with { Component = "payments" }));

        Assert.Equal("# file header comment", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.StartsWith("id:", lines[2]);
    }

    [Fact]
    public void CommentAboveAnUnknownKey_StaysWithIt_WhenTheKnownKeyBeforeItChanges()
    {
        var t = Parse(Markdown);

        var lines = FrontmatterLines(Write(t with { Status = "orphaned" }));

        Assert.Equal("# custom note", LineAbove(lines, "x_custom:"));
    }

    [Fact]
    public void TrailingComment_StaysLast()
    {
        var t = Parse(Markdown);

        var lines = FrontmatterLines(Write(t with { Status = "orphaned", Component = "payments" }));

        Assert.Equal("# end", lines[^1]);
    }

    [Fact]
    public void RemovedKey_TakesItsCommentWithIt()
    {
        var t = Parse(Markdown);

        var lines = FrontmatterLines(Write(t with { DependsOn = null }));

        Assert.DoesNotContain("# deps", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("depends_on:"));
        Assert.Equal("# refs below", LineAbove(lines, "source_refs:"));
        Assert.Equal("", lines[Array.IndexOf(lines, "# refs below") - 1]);
    }

    [Fact]
    public void UnchangedFile_InCanonicalOrder_RewritesByteIdentically()
    {
        var canonical = Write(Parse(Markdown));

        Assert.Equal(canonical, Write(Parse(canonical)));
    }

    // ---------- Spec 081 C5: a keep-chomped block value keeps its trailing lines ----------

    private const string Body = "---\n\n# Keep chomping\n\n## Steps\n\n1. Do it\n\n## Expected Result\n\nDone\n";

    [Fact]
    public void KeepChompedValue_KeepsItsTrailingLines_WhenTheNextKeyIsRemoved()
    {
        const string md = "---\nid: TC-300\npriority: high\ndescription: |+\n  first\n  second\n\n\ndepends_on: TC-001\n" + Body;
        var original = Parse(md);
        Assert.Equal("first\nsecond\n\n\n", original.Description);

        var reparsed = Parse(Write(original with { DependsOn = null }));

        Assert.Equal(original.Description, reparsed.Description);
        Assert.Null(reparsed.DependsOn);
    }

    [Fact]
    public void NestedKeepChompedValue_KeepsItsTrailingLines_WhenTheNextKeyIsRemoved()
    {
        const string md = "---\nid: TC-301\npriority: high\ncustom:\n  notes: |+\n    kept\n\n\nstatus: active\n" + Body;
        var original = Parse(md);
        Assert.Equal("kept\n\n\n", original.Custom!["notes"]);

        var reparsed = Parse(Write(original with { Status = null }));

        Assert.Equal(original.Custom!["notes"], reparsed.Custom!["notes"]);
    }

    [Fact]
    public void FoldedKeepValue_WithIndentationIndicator_KeepsItsTrailingLines()
    {
        const string md = "---\nid: TC-302\npriority: high\ndescription: >2+\n  folded\n  text\n\n\ndepends_on: TC-001\n" + Body;
        var original = Parse(md);

        var reparsed = Parse(Write(original with { DependsOn = null }));

        Assert.Equal(original.Description, reparsed.Description);
    }

    [Fact]
    public void CommentAfterAKeepChompedValue_StillMovesWithTheKeyBelow()
    {
        const string md = "---\nid: TC-303\npriority: high\ndescription: |+\n  kept\n\n# above next\ndepends_on: TC-001\n" + Body;
        var original = Parse(md);

        var written = Write(original with { DependsOn = "TC-002" });
        var lines = FrontmatterLines(written);

        Assert.Equal("# above next", LineAbove(lines, "depends_on:"));
        Assert.Equal(original.Description, Parse(written).Description);
    }

    [Fact]
    public void KeepChompedValue_UnchangedFile_RewritesByteIdentically()
    {
        const string md = "---\nid: TC-304\npriority: high\ndescription: |+\n  kept\n\n\ndepends_on: TC-001\n" + Body;
        var canonical = Write(Parse(md));

        Assert.Equal(canonical, Write(Parse(canonical)));
        Assert.Equal(md, canonical);
    }
}
