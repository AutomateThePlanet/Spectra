using Spectra.Core.Baseline;
using Spectra.Core.Text;

namespace Spectra.Core.Tests.Baseline;

/// <summary>
/// Spec 085 F1 (FR-001–FR-006, FR-001a): one heading matcher — text key after markup removal, then explicit IDs, then
/// a GitHub duplicate suffix — used by section extraction and coverage alike. T1 (matching), T2 (084 guarantee),
/// T3 (extraction and coverage agree), and the comparison-text rows.
/// </summary>
public class HeadingMatchTests
{
    private const string GitBook =
        "# Doc\n\n## Create customer records <a href=\"#create-customer-records\" id=\"create-customer-records\"></a>\n\nbody\n\n## Next\n";

    private const string Setups = "## Setup\n\nfirst\n\n## Setup\n\nsecond\n\n## Setup\n\nthird\n";

    // T1 — (document, anchor, expected outcome, expected heading line or null, expected rule or null)
    public static TheoryData<string, string, SectionOutcome, string?, MatchRule?> Rows => new()
    {
        { GitBook, "create-customer-records", SectionOutcome.Found, "## Create customer records <a href=\"#create-customer-records\" id=\"create-customer-records\"></a>", MatchRule.Text },
        { GitBook, "Create-customer-records", SectionOutcome.Found, "## Create customer records <a href=\"#create-customer-records\" id=\"create-customer-records\"></a>", MatchRule.Text },
        { "## Title {#custom-id}\n\nt\n", "custom-id", SectionOutcome.Found, "## Title {#custom-id}", MatchRule.ExplicitId },
        { "## Title {#custom-id}\n\nt\n", "title", SectionOutcome.Found, "## Title {#custom-id}", MatchRule.Text },
        { "## Title {: #custom-id}\n\nt\n", "custom-id", SectionOutcome.Found, "## Title {: #custom-id}", MatchRule.ExplicitId },
        { "## Title {#custom-id .wide}\n\nt\n", "custom-id", SectionOutcome.Found, "## Title {#custom-id .wide}", MatchRule.ExplicitId },
        { "## [Payments](payments.md)\n\np\n", "payments", SectionOutcome.Found, "## [Payments](payments.md)", MatchRule.Text },
        { Setups, "setup", SectionOutcome.Found, "## Setup", MatchRule.Text },
        { Setups, "setup-1", SectionOutcome.Found, "## Setup", MatchRule.DuplicateSuffix },
        { Setups, "setup-2", SectionOutcome.Found, "## Setup", MatchRule.DuplicateSuffix },
        { "## Overview {#intro}\n\no\n", "intro", SectionOutcome.Found, "## Overview {#intro}", MatchRule.ExplicitId },
        { "## Overview {#intro}\n\no\n\n## Intro\n\ni\n", "intro", SectionOutcome.Found, "## Intro", MatchRule.Text },
        { "## Step\n\na\n\n## Step-2\n\nb\n", "step-2", SectionOutcome.Found, "## Step-2", MatchRule.Text },
        { Setups, "setup-0", SectionOutcome.AnchorNotFound, null, null },
        { Setups, "setup-a", SectionOutcome.AnchorNotFound, null, null },
        { "## Setup\n\na\n\n## Setup\n\nb\n", "setup-5", SectionOutcome.AnchorNotFound, null, null },
        { "## Config {key}\n\nc\n", "config-key", SectionOutcome.Found, "## Config {key}", MatchRule.Text },
        { "## Single <span id='single'></span>\n\ns\n", "single", SectionOutcome.Found, "## Single <span id='single'></span>", MatchRule.Text },
        { "## Visible <span id='other'></span>\n\ns\n", "other", SectionOutcome.Found, "## Visible <span id='other'></span>", MatchRule.ExplicitId },
        { "## Plain heading\n\nx\n", "plain-heading", SectionOutcome.Found, "## Plain heading", MatchRule.Text },
        { "## **Bold** title\n\nx\n", "bold-title", SectionOutcome.Found, "## **Bold** title", MatchRule.Text },
        { "## Use `code` here\n\nx\n", "use-code-here", SectionOutcome.Found, "## Use `code` here", MatchRule.Text },
        { "## Something\n\nx\n", "nothing-like-it", SectionOutcome.AnchorNotFound, null, null },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Match_FindsTheRightHeading(string doc, string anchor, SectionOutcome outcome, string? headingLine, MatchRule? rule)
    {
        var lookup = SectionExtractor.Extract(doc, anchor);
        var match = SectionExtractor.Match(SectionExtractor.Headings(doc), anchor);

        Assert.Equal(outcome, lookup.Outcome);
        Assert.Equal(rule, match?.Rule);
        if (headingLine is not null)
        {
            Assert.StartsWith(headingLine + "\n", lookup.RawText);
        }
    }

    [Fact]
    public void DuplicateSuffix_PicksTheRightOccurrence()
    {
        Assert.Contains("first", SectionExtractor.Extract(Setups, "setup").RawText);
        Assert.Contains("second", SectionExtractor.Extract(Setups, "setup-1").RawText);
        Assert.Contains("third", SectionExtractor.Extract(Setups, "setup-2").RawText);
    }

    // T3 — extraction and coverage agree for every T1 row
    [Theory]
    [MemberData(nameof(Rows))]
    public void CoveredHeadings_AgreeWithExtraction(string doc, string anchor, SectionOutcome outcome, string? headingLine, MatchRule? rule)
    {
        _ = headingLine;
        _ = rule;
        var headings = SectionExtractor.Headings(doc);
        var covered = SectionExtractor.CoveredHeadings(headings, anchor);
        var match = SectionExtractor.Match(headings, anchor);

        if (outcome == SectionOutcome.AnchorNotFound)
        {
            Assert.Null(match);
            Assert.Empty(covered);
        }
        else
        {
            Assert.NotNull(match);
            Assert.Equal(match.Index, covered[0]);
        }
    }

    [Theory]
    [InlineData(GitBook, 1, "Create customer records")]
    [InlineData("## Title {#custom-id}\n", 0, "Title")]
    [InlineData("## [Payments](payments.md)\n", 0, "Payments")]
    [InlineData("## **Bold** Title\n", 0, "Bold Title")]
    public void CleanText_IsTheHeadingWithoutMarkup_InTheDocumentsCase(string doc, int index, string clean) =>
        Assert.Equal(clean, SectionExtractor.Headings(doc)[index].CleanText);

    // FR-001a — a heading attribute block is not part of the comparison text
    [Theory]
    [InlineData("## Quotas {#quotas}\n\ntext\n", "## Quotas\n\ntext\n")]
    [InlineData("## Quotas {: #quotas}\n\ntext\n", "## Quotas\n\ntext\n")]
    [InlineData("## Quotas {.cls}\n\ntext\n", "## Quotas\n\ntext\n")]
    [InlineData("## Parent\n\np\n\n### Child {#child}\n\nc\n", "## Parent\n\np\n\n### Child\n\nc\n")]
    [InlineData("## Quotas <a id=\"quotas\"></a>\n\ntext\n", "## Quotas\n\ntext\n")]
    [InlineData("## [Quotas](quotas.md)\n\ntext\n", "## Quotas\n\ntext\n")]
    public void ComparisonText_IgnoresHeadingAttributeBlocksAndMarkup(string withMarkup, string plain) =>
        Assert.Equal(SectionExtractor.ComparisonText(plain), SectionExtractor.ComparisonText(withMarkup));

    [Fact]
    public void ComparisonText_KeepsABlockInsideFencedCode() =>
        Assert.NotEqual(
            SectionExtractor.ComparisonText("## A\n\n```\n## Fake {#x}\n```\n"),
            SectionExtractor.ComparisonText("## A\n\n```\n## Fake\n```\n"));

    [Fact]
    public void ComparisonText_OfAPlainSection_IsTheNormalisedText() =>
        Assert.Equal(TextNormalizer.Normalize("## Quotas\n\nEach **user** gets 10GB.\n"),
            SectionExtractor.ComparisonText("## Quotas\n\nEach **user** gets 10GB.\n"));

    // T2 — the 084 guarantee: refs to plain, emphasised and inline-code headings resolve to the same section
    private const string GuaranteeDoc =
        "# Guide\n\nintro\n\n## Plain heading\n\na\n\n## **Bold** heading\n\nb\n\n## *Em* heading\n\nc\n\n"
        + "## _Under_ heading\n\nd\n\n## Use `code` here\n\ne\n\n### Nested `x` part\n\nf\n\n## Last one\n\ng\n";

    public static TheoryData<string> GuaranteeAnchors => new()
    {
        "Guide", "guide", "Plain-heading", "plain-heading", "Plain-Heading",
        "Bold-heading", "bold-heading", "Em-heading", "em-heading", "Under-heading", "under-heading",
        "Use-code-here", "use-code-here", "Nested-x-part", "nested-x-part", "Last-one", "last-one"
    };

    [Theory]
    [MemberData(nameof(GuaranteeAnchors))]
    public void Guarantee_SameSectionAndFingerprintAs084(string anchor)
    {
        var old = Extract084(GuaranteeDoc, anchor);
        var now = SectionExtractor.Extract(GuaranteeDoc, anchor);

        Assert.NotNull(old);
        Assert.Equal(SectionOutcome.Found, now.Outcome);
        Assert.Equal(old, now.RawText);
        Assert.Equal(SourceFingerprint.OfSection(old!), SourceFingerprint.OfSection(now.RawText!));
    }

    /// <summary>A private copy of the 084 rule: the first heading whose RAW text key equals the anchor key.</summary>
    private static string? Extract084(string text, string anchor)
    {
        var normalised = SectionExtractor.NormalizeNewlines(text);
        var key = AnchorKey.FromAnchor(anchor);
        var headings = SectionExtractor.Headings(normalised);
        var index = headings.ToList().FindIndex(h => AnchorKey.Make(h.Text) == key);
        if (index < 0) return null;
        var lines = normalised.Split('\n');
        var startLine = headings[index].StartLine;
        var endLine = headings.Skip(index + 1).FirstOrDefault(h => h.Level <= headings[index].Level)?.StartLine ?? lines.Length;
        var section = string.Join("\n", lines[startLine..endLine]);
        return endLine < lines.Length ? section + "\n" : section;
    }
}
