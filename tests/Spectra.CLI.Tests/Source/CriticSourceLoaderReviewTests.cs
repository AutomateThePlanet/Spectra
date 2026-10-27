using Spectra.CLI.Source;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Baseline;
using Spectra.Core.Coverage;

namespace Spectra.CLI.Tests.Source;

/// <summary>
/// Spec 102 (F2, F5, F7; FR-007, FR-008, FR-014, FR-016; research R3, R7): the critic loader groups refs by file with
/// the resolver's comparer, so refs that differ only in case reach the critic together (the comparer is injected, so
/// the rows run on every OS); the ambiguous-anchor warning is only for a section that is given; and each document is
/// matched once per ref and sliced once. On 26bfe88 grouping was ordinal (on Windows <c>docs/Auth.md#login</c> +
/// <c>docs/auth.md#logout</c> gave the critic only Login), the warning came for absorbed sections too, and the matcher
/// ran about n² times per document.
/// </summary>
public sealed class CriticSourceLoaderReviewTests : IDisposable
{
    private const string AuthDoc = "# Auth\n\n## Login\n\nLogin body.\n\n## Logout\n\nLogout body.\n";

    private readonly BaselineTestWorkspace _ws = new();
    private readonly List<string> _notes = [];

    public CriticSourceLoaderReviewTests() => _ws.WriteDoc("docs/auth.md", AuthDoc);

    public void Dispose() => _ws.Dispose();

    private SourceRefResolution MixedCase(string secondRef, string secondRelative)
    {
        var onDisk = _ws.PathOf("docs/auth.md");
        return new SourceRefResolution(
            [
                new ResolvedSourceRef("docs/auth.md#login", "docs/auth.md", onDisk),
                new ResolvedSourceRef(secondRef, secondRelative, _ws.PathOf(secondRelative)),
            ],
            [],
            [onDisk]);
    }

    private Task<IReadOnlyList<Spectra.Core.Models.SourceDocument>> LoadAsync(params string[] refs) =>
        CriticSourceLoader.LoadAsync(refs, _ws.Root, "docs", _notes.Add, CancellationToken.None);

    [Fact]
    public async Task MixedCaseAnchors_GiveBothSections_UnderTheCaseInsensitiveComparer()
    {
        var docs = await CriticSourceLoader.LoadAsync(
            MixedCase("docs/Auth.md#logout", "docs/Auth.md"), StringComparer.OrdinalIgnoreCase, _notes.Add, CancellationToken.None);

        var doc = Assert.Single(docs);
        Assert.Equal("## Login\n\nLogin body.\n\n## Logout\n\nLogout body.\n", doc.Content);
    }

    [Fact]
    public async Task AMixedCaseAnchorlessRef_GivesTheWholeFile_UnderTheCaseInsensitiveComparer()
    {
        var docs = await CriticSourceLoader.LoadAsync(
            MixedCase("docs/Auth.md", "docs/Auth.md"), StringComparer.OrdinalIgnoreCase, _notes.Add, CancellationToken.None);

        Assert.Equal(AuthDoc, Assert.Single(docs).Content);
    }

    [CaseInsensitiveFsFact]
    public async Task ThePublicLoader_GroupsMixedCaseRefs()
    {
        var anchored = Assert.Single(await LoadAsync("docs/Auth.md#login", "docs/auth.md#logout"));
        Assert.Contains("Login body.", anchored.Content);
        Assert.Contains("Logout body.", anchored.Content);

        var whole = Assert.Single(await LoadAsync("docs/Auth.md#login", "docs/auth.md"));
        Assert.Equal(AuthDoc, whole.Content);
    }

    [Fact]
    public void TheResolversComparer_IsOrdinalOnLinux_ElseIgnoreCase()
    {
        Assert.Same(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase, SourceRefResolver.PathComparer);
    }

    [Fact]
    public async Task AnAmbiguousAnchor_WhoseSectionIsGiven_IsNoted()
    {
        _ws.WriteDoc("docs/d.md", "# D\n\n## Setup\n\nFirst.\n\n## Setup\n\nSecond.\n\n## Other\n\nOther.\n");

        await LoadAsync("docs/d.md#setup");

        Assert.Contains(_notes, n => n.Contains("more than one heading shares the anchor key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAmbiguousAnchor_AbsorbedIntoAnotherCitedSection_IsNotNoted()
    {
        const string text = "# D\n\n## Parent\n\nP.\n\n### Setup\n\nA.\n\n### Setup\n\nB.\n";
        _ws.WriteDoc("docs/d.md", text);

        var doc = Assert.Single(await LoadAsync("docs/d.md#parent", "docs/d.md#setup"));

        Assert.Equal("## Parent\n\nP.\n\n### Setup\n\nA.\n\n### Setup\n\nB.\n", doc.Content);
        Assert.Empty(_notes);
    }

    private static string CountDoc()
    {
        var text = "# Feature\n";
        for (var i = 1; i <= 8; i++)
        {
            text += $"\n## S{i}\n\nS{i} body.\n";
            if (i == 3)
            {
                text += "\n### S3a\n\nS3a body.\n\n### S3b\n\nS3b body.\n";
            }
        }

        return text;
    }

    private static string Section(string text, string anchor) =>
        SectionExtractor.Extract(text, anchor).RawText!.TrimEnd('\n');

    [Fact]
    public void EachRef_IsMatchedOnce_AndTheDocumentIsSlicedOnce()
    {
        var text = CountDoc();
        string[] refs = ["d.md#s3", "d.md#s3a", "d.md#s1", "d.md#s5", "d.md#s3b", "d.md#s8"];
        int matchCalls = 0, lineStartCalls = 0;

        var content = CriticSourceLoader.Content(text, refs, _notes.Add,
            (headings, anchor) => { matchCalls++; return SectionExtractor.Match(headings, anchor); },
            t => { lineStartCalls++; return SectionExtractor.LineStarts(t); });

        Assert.Equal(6, matchCalls);
        Assert.Equal(1, lineStartCalls);
        var expected = string.Join("\n\n", new[] { "s3", "s1", "s5", "s8" }.Select(a => Section(text, a))) + "\n";
        Assert.Equal(expected, content);
    }

    [Fact]
    public void AWholeFileRef_OrAnUnmatchedAnchor_ScansNoLineStarts()
    {
        var text = CountDoc();
        int matchCalls = 0, lineStartCalls = 0;
        HeadingMatch? Match(IReadOnlyList<MarkdownHeading> h, string? a) { matchCalls++; return SectionExtractor.Match(h, a); }
        IReadOnlyList<int> LineStarts(string t) { lineStartCalls++; return SectionExtractor.LineStarts(t); }

        Assert.Equal(text, CriticSourceLoader.Content(text, ["d.md"], null, Match, LineStarts));
        Assert.Equal(0, matchCalls);
        Assert.Equal(0, lineStartCalls);

        CriticSourceLoader.Content(text, ["d.md#nope"], null, Match, LineStarts);
        Assert.Equal(0, lineStartCalls);
    }
}

/// <summary>
/// Spec 102 F2 (FR-008; analysis I3): the bug is in <c>compile-critic-prompt</c>'s path, so one row goes through the
/// command with refs that differ only in case. Needs a case-insensitive file system (skipped on Linux).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CriticMixedCaseCommandTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [CaseInsensitiveFsFact]
    public async Task CompileCriticPrompt_GivesBothSections_ForMixedCaseRefs()
    {
        _ws.WriteDoc("docs/auth.md", "# Auth\n\n## Login\n\nLogin body.\n\n## Logout\n\nLogout body.\n");
        _ws.WriteTest(CriticSourceWorkspace.Suite, "TC-010", ["docs/Auth.md#login", "docs/auth.md#logout"], title: "Check the cited section");

        var (exit, output, error) = await _ws.RunAsync("ai", "compile-critic-prompt", "--suite", CriticSourceWorkspace.Suite, "--test", "TC-010");

        Assert.True(exit == 0, $"exit {exit}: {error}");
        Assert.Contains("Login body.", output);
        Assert.Contains("Logout body.", output);
    }
}
