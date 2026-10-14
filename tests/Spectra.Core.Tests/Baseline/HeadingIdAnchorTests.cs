using Spectra.Core.Baseline;
using Spectra.Core.Text;

namespace Spectra.Core.Tests.Baseline;

/// <summary>
/// Spec 098 F6 (FR-013–FR-016, SC-006): <c>id=</c> counts as a heading id only as a whole attribute name; a heading
/// with an image also matches GitHub's anchor (an image renders no text); an 084 snapshot taken while a heading had a
/// <c>{#id}</c> block compares as cosmetic when only that block changed. On 986da01 <c>data-id="x"</c> was an id,
/// <c>#setup</c> didn't match <c>## ![logo](x.png) Setup</c>, and the 084 snapshot compared as LIGHT.
/// </summary>
public class HeadingIdAnchorTests
{
    private static SectionOutcome Outcome(string doc, string anchor) => SectionExtractor.Extract(doc, anchor).Outcome;

    [Theory]
    [InlineData("## <span data-id=\"x\">Setup</span>\n\nbody\n")]
    [InlineData("## <span aria-id=\"x\">Setup</span>\n\nbody\n")]
    [InlineData("## <span x-id='x'>Setup</span>\n\nbody\n")]
    [InlineData("## <span :id=\"x\">Setup</span>\n\nbody\n")]
    public void APrefixedIdAttribute_IsNotAHeadingId(string doc)
    {
        Assert.Equal(SectionOutcome.AnchorNotFound, Outcome(doc, "x"));
        Assert.Empty(SectionExtractor.Headings(doc)[0].ExplicitIds);
    }

    [Theory]
    [InlineData("## <a id=\"x\"></a>Setup\n\nbody\n")]
    [InlineData("## Setup <a name=\"y\" id='x'></a>\n\nbody\n")]
    [InlineData("## <a ID = \"x\"></a>Setup\n\nbody\n")]
    [InlineData("## id=\"x\" Setup\n\nbody\n")]
    [InlineData("## Setup {#x}\n\nbody\n")]
    public void AWholeIdAttribute_IsStillAHeadingId(string doc)
    {
        Assert.Equal(SectionOutcome.Found, Outcome(doc, "x"));
    }

    [Fact]
    public void TheCanonicalRef_IgnoresPrefixedIdAttributes()
    {
        var headings = SectionExtractor.Headings("# Doc\n\n## <span data-id=\"x\">Setup</span>\n\nbody\n");

        Assert.Equal("docs/a.md#setup", SectionRef.Canonical("docs/a.md", headings, 1));
    }

    [Fact]
    public void TheCanonicalRef_StillUsesAWholeIdAttribute()
    {
        var headings = SectionExtractor.Headings("# Doc\n\n## <a id=\"x\"></a>Setup\n\nbody\n");

        Assert.Equal("docs/a.md#x", SectionRef.Canonical("docs/a.md", headings, 1));
    }

    [Theory]
    [InlineData("setup")]
    [InlineData("-setup")]
    [InlineData("logo-setup")]
    public void AnImageHeading_MatchesGitHubsAnchorAndThe085One(string anchor)
    {
        const string doc = "# Doc\n\n## ![logo](x.png) Setup\n\nbody\n\n## Next\n";

        var lookup = SectionExtractor.Extract(doc, anchor);

        Assert.Equal(SectionOutcome.Found, lookup.Outcome);
        Assert.StartsWith("## ![logo](x.png) Setup", lookup.RawText);
    }

    [Fact]
    public void AnImageOnlyHeading_MatchesOnlyBy085Key()
    {
        const string doc = "## ![logo](x.png)\n\nbody\n";

        Assert.Equal(SectionOutcome.Found, Outcome(doc, "logo"));
        Assert.Equal(SectionOutcome.AnchorNotFound, Outcome(doc, "x"));
    }

    [Fact]
    public void AHeadingWhoseOwnTextMatches_WinsOverAnImageHeading()
    {
        const string doc = "## ![logo](x.png) Setup\n\nfirst\n\n## Setup\n\nsecond\n";

        var lookup = SectionExtractor.Extract(doc, "setup");

        Assert.Equal(SectionOutcome.Found, lookup.Outcome);
        Assert.StartsWith("## Setup\n\nsecond", lookup.RawText);
        Assert.False(lookup.Ambiguous);
    }

    // ---- 084 snapshot with {#id} ----

    private static RefVerdict Classify(string previousRaw, string currentRaw, bool snapshotFrom084)
    {
        var snapshot = snapshotFrom084 ? TextNormalizer.Normalize(previousRaw) : SectionExtractor.ComparisonText(previousRaw);
        var current = CurrentSection.FromRaw(currentRaw);
        return ChangeClassifier.ClassifyRef("docs/a.md#setup", SourceFingerprint.OfSection(previousRaw), current, snapshot, 12);
    }

    [Theory]
    [InlineData("## Setup {#setup}\n\nInstall the tool.\n", "## Setup\n\nInstall the tool.\n")]
    [InlineData("## Setup {#setup}\n\nInstall the tool.\n", "## Setup {#install}\n\nInstall the tool.\n")]
    [InlineData("## Setup {: #setup .lead}\n\nInstall the tool.\n", "## Setup\n\nInstall the tool.\n")]
    [InlineData("## Setup\n\nInstall the tool.\n\n### Details {#d}\n\nMore.\n", "## Setup\n\nInstall the tool.\n\n### Details\n\nMore.\n")]
    public void An084SnapshotWithAHeadingId_IdOnlyChange_IsCosmetic(string previous, string current)
    {
        Assert.Equal(RefState.Cosmetic, Classify(previous, current, snapshotFrom084: true).State);
    }

    [Fact]
    public void An084Snapshot_ARealEdit_IsStillAChange()
    {
        var verdict = Classify("## Setup {#setup}\n\nInstall the tool.\n", "## Setup\n\nInstall the new tool.\n", snapshotFrom084: true);

        Assert.NotEqual(RefState.Cosmetic, verdict.State);
    }

    [Fact]
    public void BracesInBodyText_AreNeverStrippedFromASnapshot()
    {
        var verdict = Classify("## Setup\n\nUse {#each} here.\n", "## Setup\n\nUse here.\n", snapshotFrom084: true);

        Assert.NotEqual(RefState.Cosmetic, verdict.State);
    }

    [Fact]
    public void A085Snapshot_IsUnaffected()
    {
        Assert.Equal(RefState.Cosmetic,
            Classify("## Setup\n\nInstall the tool.\n", "## Setup {#setup}\n\nInstall the tool.\n", snapshotFrom084: false).State);
    }
}
