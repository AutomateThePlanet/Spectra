using Spectra.CLI.Source;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Baseline;

namespace Spectra.CLI.Tests.Source;

/// <summary>
/// Spec 101 F1 (FR-001, FR-002; research R1): the critic's documents are the sections a test cites — one item per
/// document, first-reference order, the 085 matcher, 094's absorption, the whole file for an anchor-less ref, and the
/// whole file with a note for an anchor that matches no heading. On 6a4c41a <c>CriticSourceLoader</c> doesn't exist
/// (the critic got each referenced file whole).
/// </summary>
public sealed class CriticSourceLoaderTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();
    private readonly List<string> _notes = [];

    public CriticSourceLoaderTests() => _ws.WriteCriticDocs();

    public void Dispose() => _ws.Dispose();

    private Task<IReadOnlyList<Spectra.Core.Models.SourceDocument>> LoadAsync(params string[] refs) =>
        CriticSourceLoader.LoadAsync(refs, _ws.Root, "docs", _notes.Add, CancellationToken.None);

    private static string Section(string anchor) =>
        SectionExtractor.Extract(CriticSourceWorkspace.LongDocText(), anchor).RawText!.TrimEnd('\n');

    [Fact]
    public async Task TwoAnchors_GiveOneItem_WithBothSections_InFirstReferenceOrder()
    {
        var docs = await LoadAsync("docs/long.md#section-30", "docs/long.md#section-2");

        var doc = Assert.Single(docs);
        Assert.Equal("docs/long.md", doc.Path);
        Assert.Equal("long", doc.Title);
        Assert.Equal(Section("section-30") + "\n\n" + Section("section-2") + "\n", doc.Content);
        Assert.DoesNotContain("## Section 29", doc.Content);
        Assert.DoesNotContain("Intro line", doc.Content);
    }

    [Fact]
    public async Task ASection_IncludesItsSubsections_AndAbsorbsACitedSubsection()
    {
        var docs = await LoadAsync("docs/long.md#section-12", "docs/long.md#sub-121");

        var content = Assert.Single(docs).Content;
        Assert.Contains("### Sub 12.1", content);
        Assert.Equal(Section("section-12") + "\n", content);
    }

    [Fact]
    public async Task TheSameSection_CitedTwice_AppearsOnce()
    {
        var docs = await LoadAsync("docs/long.md#section-2", "docs/long.md#Section-2");

        Assert.Equal(Section("section-2") + "\n", Assert.Single(docs).Content);
    }

    [Fact]
    public async Task AWholeFileRef_GivesTheWholeFile_AsItIsOnDisk()
    {
        var docs = await LoadAsync("docs/long.md", "docs/long.md#section-2");

        Assert.Equal(File.ReadAllText(_ws.PathOf("docs/long.md")), Assert.Single(docs).Content);
    }

    [Fact]
    public async Task AnUnmatchedAnchor_GivesTheWholeFile_WithANoteLine()
    {
        var docs = await LoadAsync("docs/long.md#removed", "docs/long.md#section-2", "docs/long.md#gone");

        var content = Assert.Single(docs).Content;
        Assert.StartsWith(
            "> Note: the anchor \"#removed\" was not found in this document, so the whole document is given.\n"
            + "> Note: the anchor \"#gone\" was not found in this document, so the whole document is given.\n",
            content);
        Assert.EndsWith(File.ReadAllText(_ws.PathOf("docs/long.md")), content);
    }

    [Fact]
    public async Task TwoDocuments_GiveTwoItems_InFirstReferenceOrder()
    {
        var docs = await LoadAsync("docs/short.md", "docs/long.md#section-2");

        Assert.Equal(["docs/short.md", "docs/long.md"], docs.Select(d => d.Path));
        Assert.Equal(["short", "long"], docs.Select(d => d.Title));
        Assert.Equal(CriticSourceWorkspace.ShortDocText, docs[0].Content);
    }

    [Fact]
    public async Task AMissingFile_IsRefused_WithTheSameMessage()
    {
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => LoadAsync("docs/missing.md#x"));
        Assert.Equal("Source ref not found: docs/missing.md#x", ex.Message);
    }

    [Fact]
    public async Task AnOutsideRootRef_IsRefused_WithTheSameMessage()
    {
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => LoadAsync("../../outside.md"));
        Assert.StartsWith("Source ref is outside the repository and the configured source directory", ex.Message);
    }

    [Fact]
    public async Task AnEmptySection_GivesItsHeadingOnly_WithNoFallback()
    {
        _ws.WriteDoc("docs/empty.md", "# Doc\n\nIntro.\n\n## Empty\n## Next\n\nNext body.\n");

        var docs = await LoadAsync("docs/empty.md#empty");

        Assert.Equal("## Empty\n", Assert.Single(docs).Content);
    }

    [Fact]
    public async Task AnAmbiguousAnchor_GivesTheFirstHeading_AndANote()
    {
        _ws.WriteDoc("docs/dup.md", "# Doc\n\n## Setup\n\nFirst setup.\n\n## Setup\n\nSecond setup.\n");

        var docs = await LoadAsync("docs/dup.md#setup");

        Assert.Equal("## Setup\n\nFirst setup.\n", Assert.Single(docs).Content);
        Assert.Contains(_notes, n => n.Contains("more than one heading shares the anchor key 'setup'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheSectionsKept_AreTheOnes094sAbsorptionKeeps()
    {
        var refs = new[] { "docs/long.md#section-12", "docs/long.md#sub-121", "docs/long.md#section-2" };
        var headings = SectionExtractor.Headings(CriticSourceWorkspace.LongDocText());
        var kept = refs.Where((r, i) =>
        {
            var position = SectionExtractor.Match(headings, SectionExtractor.AnchorOf(r))!.Index;
            return !refs.Where((_, j) => j != i).Any(other =>
            {
                var otherPosition = SectionExtractor.Match(headings, SectionExtractor.AnchorOf(other))!.Index;
                return otherPosition != position
                       && SectionExtractor.CoveredHeadings(headings, SectionExtractor.AnchorOf(other)).Contains(position);
            });
        }).Select(r => Section(SectionExtractor.AnchorOf(r)!));

        var docs = await LoadAsync(refs);

        Assert.Equal(string.Join("\n\n", kept) + "\n", Assert.Single(docs).Content);
    }
}
