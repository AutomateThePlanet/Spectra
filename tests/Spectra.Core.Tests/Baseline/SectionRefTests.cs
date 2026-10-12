using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>
/// Spec 094 T1 (R1): the canonical ref the generation plan stores for a section — the repo-relative path for a whole
/// document, else <c>path#anchor</c> with the heading's explicit id or a GitHub-style slug (<c>-n</c> for the n-th
/// duplicate) — and it always resolves back to the same heading through the 085 matcher. On 55b541f the type didn't exist.
/// </summary>
public class SectionRefTests
{
    private const string Doc =
        "# Storage\n\n## Upload quota\n\ntext\n\n### Quota display\n\nmore\n\n## Limits {#quota-limits}\n\nx\n\n"
        + "## Limits\n\ny\n\n## Limits\n\nz\n\n## Über uns\n\nü\n\n## The `--force` flag (v2.0)!\n\nf\n\n## ---\n\nnothing\n";

    private static IReadOnlyList<MarkdownHeading> Headings => SectionExtractor.Headings(Doc);

    private static int IndexOf(string text, int occurrence = 0) =>
        Enumerable.Range(0, Headings.Count).Where(i => Headings[i].Text == text).ElementAt(occurrence);

    [Fact]
    public void AWholeDocument_IsItsPath()
    {
        Assert.Equal("docs/storage.md", SectionRef.Canonical("docs/storage.md", Headings, null));
    }

    [Theory]
    [InlineData("Upload quota", 0, "docs/storage.md#upload-quota")]
    [InlineData("Quota display", 0, "docs/storage.md#quota-display")]
    [InlineData("Limits {#quota-limits}", 0, "docs/storage.md#quota-limits")]
    [InlineData("Limits", 0, "docs/storage.md#limits-1")]
    [InlineData("Limits", 1, "docs/storage.md#limits-2")]
    [InlineData("Über uns", 0, "docs/storage.md#über-uns")]
    public void ASection_IsPathHashAnchor(string heading, int occurrence, string expected)
    {
        Assert.Equal(expected, SectionRef.Canonical("docs/storage.md", Headings, IndexOf(heading, occurrence)));
    }

    [Fact]
    public void EveryCanonicalAnchor_ResolvesBackToItsOwnHeading()
    {
        var headings = Headings;
        for (var i = 0; i < headings.Count; i++)
        {
            var canonical = SectionRef.Canonical("docs/storage.md", headings, i);
            if (canonical is null)
            {
                continue;
            }

            var anchor = SectionExtractor.AnchorOf(canonical);
            Assert.Equal(i, SectionExtractor.Match(headings, anchor)!.Index);
        }
    }

    [Fact]
    public void AHeadingWithNoLettersOrDigits_HasNoCanonicalRef()
    {
        Assert.Null(SectionRef.Canonical("docs/storage.md", Headings, IndexOf("---")));
    }
}
