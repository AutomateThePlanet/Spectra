using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Baseline;
using Spectra.Core.Diagnostics;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Baseline;

/// <summary>
/// Spec 099 F2 (FR-007/FR-008, SC-003): a <see cref="BaselineContext"/> scans each document's headings once, however
/// many refs point into it and however often its headings or raw sections are asked for. Before 099 every ref and every
/// heading lookup re-scanned the document. Results are the same as <see cref="SectionExtractor.Extract"/>.
/// </summary>
public sealed class HeadingScanCountTests : IDisposable
{
    private const string Checkout =
        "# Checkout {#checkout}\n\nIntro.\n\n## Card payments {#cards}\n\nCharged in EUR.\n\n"
        + "```bash\n# not a heading\n```\n\n"
        + "## Limits\n\nFirst.\n\n## Limits\n\nSecond.\n\n"
        + "## ![logo](x.png) Setup\n\nImage heading.\n\n"
        + "### Card settings\n\nNested.\n";

    private const string Refunds = "# Refunds\n\n## Partial refunds\n\nCapped.\n";
    private const string Receipts = "# Receipts\n\n## Currency\n\nShown.\n";

    private static readonly string[] CheckoutAnchors =
        ["checkout", "cards", "card-payments", "limits", "limits-1", "setup", "-setup", "logo-setup", "card-settings", "nope"];

    private readonly BaselineTestWorkspace _ws = new();

    public HeadingScanCountTests()
    {
        _ws.WriteDoc("docs/checkout.md", Checkout);
        _ws.WriteDoc("docs/refunds.md", Refunds);
        _ws.WriteDoc("docs/receipts.md", Receipts);
    }

    public void Dispose() => _ws.Dispose();

    private Task<BaselineContext> ContextAsync() => BaselineContext.CreateAsync(_ws.Root, SpectraConfig.Default, CancellationToken.None);

    [Fact]
    public async Task ManyRefsIntoOneDocument_ScanItOnce()
    {
        var context = await ContextAsync();
        using var scope = ParseCounter.Begin();

        for (var i = 0; i < 5; i++)
        {
            foreach (var anchor in CheckoutAnchors)
            {
                await context.GetSectionAsync($"docs/checkout.md#{anchor}", CancellationToken.None);
                await context.GetRawSectionAsync($"docs/checkout.md#{anchor}", CancellationToken.None);
            }
        }

        Assert.Equal(1, scope.DocumentHeadingScans);
    }

    [Fact]
    public async Task RefsIntoThreeDocuments_AndHeadingLookups_ScanEachOnce()
    {
        var context = await ContextAsync();
        using var scope = ParseCounter.Begin();

        foreach (var doc in new[] { "checkout", "refunds", "receipts" })
        {
            var section = await context.GetSectionAsync($"docs/{doc}.md#{(doc == "checkout" ? "cards" : doc == "refunds" ? "partial-refunds" : "currency")}", CancellationToken.None);
            for (var i = 0; i < 3; i++)
            {
                await context.GetHeadingsAsync(section.FullPath!, CancellationToken.None);
                await context.GetRawSectionAsync(section.Ref, CancellationToken.None);
            }
        }

        Assert.Equal(3, scope.DocumentHeadingScans);
    }

    [Fact]
    public async Task MissingDocument_IsNotScanned_AndKeepsItsReason()
    {
        var context = await ContextAsync();
        using var scope = ParseCounter.Begin();

        var section = await context.GetSectionAsync("docs/gone.md#x", CancellationToken.None);

        Assert.True(section.Current.Missing);
        Assert.Equal(BaselineContext.NotFound, section.Current.MissingReason);
        Assert.Null(await context.GetRawSectionAsync("docs/gone.md#x", CancellationToken.None));
        Assert.Equal(0, scope.DocumentHeadingScans);
    }

    [Fact]
    public async Task EachContext_ScansOnce()
    {
        using var scope = ParseCounter.Begin();
        foreach (var _ in new[] { 1, 2 })
        {
            var context = await ContextAsync();
            await context.GetSectionAsync("docs/checkout.md#cards", CancellationToken.None);
            await context.GetSectionAsync("docs/checkout.md#limits", CancellationToken.None);
        }

        Assert.Equal(2, scope.DocumentHeadingScans);
    }

    [Fact]
    public async Task Sections_AreTheExtractorsResult()
    {
        var context = await ContextAsync();
        foreach (var anchor in CheckoutAnchors)
        {
            var reference = $"docs/checkout.md#{anchor}";
            var expected = SectionExtractor.Extract(Checkout, anchor);
            var raw = await context.GetRawSectionAsync(reference, CancellationToken.None);
            var section = await context.GetSectionAsync(reference, CancellationToken.None);

            Assert.Equal(expected.Outcome == SectionOutcome.AnchorNotFound ? null : expected.RawText, raw);
            Assert.Equal(expected.Ambiguous, section.Ambiguous);
            Assert.Equal(expected.Outcome == SectionOutcome.AnchorNotFound, section.Current.Missing);
        }

        var whole = await context.GetRawSectionAsync("docs/checkout.md", CancellationToken.None);
        Assert.Equal(SectionExtractor.Extract(Checkout, null).RawText, whole);

        var headings = await context.GetHeadingsAsync(_ws.PathOf("docs/checkout.md"), CancellationToken.None);
        Assert.Equal(SectionExtractor.Headings(Checkout), headings);
    }

    [Fact]
    public async Task DoubleBomDocument_KeepsBothOfItsReadings()
    {
        // Reading strips the first BOM; section extraction then removes both of the remaining two, the heading lookup
        // one — as before 099 (expected values computed the old way from the text as read).
        File.WriteAllText(_ws.PathOf("docs/bom.md"), "﻿﻿﻿## First\n\nA.\n\n## Second\n\nB.\n", new System.Text.UTF8Encoding(false));
        var read = File.ReadAllText(_ws.PathOf("docs/bom.md"));
        Assert.StartsWith("﻿﻿", read);
        var context = await ContextAsync();

        var section = await context.GetRawSectionAsync("docs/bom.md#first", CancellationToken.None);
        var headings = await context.GetHeadingsAsync(_ws.PathOf("docs/bom.md"), CancellationToken.None);

        Assert.Equal(SectionExtractor.Extract(read, "first").Outcome == SectionOutcome.AnchorNotFound ? null : SectionExtractor.Extract(read, "first").RawText, section);
        Assert.Equal(SectionExtractor.Headings(read), headings);
    }
}
