using Spectra.CLI.Baseline;
using Spectra.CLI.Generation;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 088 T1/T2 (FR-001–FR-006): what the update prompt's context says about each ref and linked criterion of the
/// tests — built from the same machinery as triage — and when a test has nothing to reconcile against.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateContextBuilderTests : IDisposable
{
    private const string Storage =
        "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space. Users must log in first.\n\n"
        + "## Sharing\n\nFiles can be shared by link.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public UpdateContextBuilderTests()
    {
        _ws.WriteDoc("docs/storage.md", Storage);
        _ws.WriteCriteria("storage.criteria.yaml",
            ("AC-1", "Each user MUST get 30GB"), ("AC-2", "Files MAY be shared by link"), ("AC-OTHER", "Unrelated rule"));
    }

    public void Dispose() => _ws.Dispose();

    private async Task<UpdateContext> BuildAsync(params string[] ids)
    {
        var config = SpectraConfig.Default;
        var testsPath = _ws.PathOf("test-cases");
        var scan = await BaselineStore.ScanSuiteAsync(_ws.Root, testsPath, "storage", CancellationToken.None);
        var context = await BaselineContext.CreateAsync(_ws.Root, config, CancellationToken.None);
        var baseline = await BaselineStore.LoadSuiteAsync(_ws.Root, testsPath, "storage", CancellationToken.None);
        var tests = scan.Tests.Where(t => ids.Contains(t.Id)).ToList();
        return await UpdateContextBuilder.BuildAsync(tests, baseline, context, 12, CancellationToken.None);
    }

    private static SectionBlock Section(UpdateContext ctx, string sourceRef) => Assert.Single(ctx.Sections, s => s.Ref == sourceRef);

    private void Edit(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    [Fact]
    public async Task ChangedRef_HasCurrentPreviousAndWhatChanged()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        Edit("30GB", "45GB");

        var section = Section(await BuildAsync("TC-101"), "docs/storage.md#Quotas");

        Assert.Equal(SectionState.Changed, section.State);
        Assert.Equal("LOAD", section.Bucket);
        Assert.Contains("**45GB**", section.Current);           // raw text, markup kept
        Assert.StartsWith("## Quotas", section.Current);
        Assert.Contains("30gb", section.Previous);              // the normalised snapshot
        Assert.DoesNotContain("**", section.Previous);
        Assert.Contains("numbers: 30gb→45gb", section.Why);
        Assert.NotEmpty(section.Diff);
        Assert.Equal(["TC-101"], section.Tests);
    }

    [Fact]
    public async Task UnchangedRef_HasCurrentOnly()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Sharing"]);
        await _ws.InitBaselineAsync();

        var section = Section(await BuildAsync("TC-101"), "docs/storage.md#Sharing");

        Assert.Equal(SectionState.Unchanged, section.State);
        Assert.Contains("Files can be shared by link.", section.Current);
        Assert.Null(section.Previous);
        Assert.Empty(section.Diff);
    }

    [Fact]
    public async Task UnbaselinedRef_HasCurrent_PreviousUnavailable()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]); // no baseline at all

        var section = Section(await BuildAsync("TC-101"), "docs/storage.md#Quotas");

        Assert.Equal(SectionState.NotBaselined, section.State);
        Assert.NotNull(section.Current);
        Assert.Null(section.Previous);
        Assert.Equal("not baselined", section.PreviousUnavailableReason);
    }

    [Fact]
    public async Task MissingSnapshot_IsChangedWithoutPreviousOrDiff()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        foreach (var f in Directory.GetFiles(_ws.PathOf(".spectra/source-snapshots"))) File.Delete(f);
        Edit("30GB", "45GB");

        var section = Section(await BuildAsync("TC-101"), "docs/storage.md#Quotas");

        Assert.Equal(SectionState.Changed, section.State);
        Assert.Null(section.Previous);
        Assert.Equal("no snapshot", section.PreviousUnavailableReason);
        Assert.Contains("no diff: snapshot missing", section.Why);
    }

    [Fact]
    public async Task AnchorlessRef_IsTheWholeFile()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md"]);
        await _ws.InitBaselineAsync();

        var section = Section(await BuildAsync("TC-101"), "docs/storage.md");

        Assert.Equal(Storage, section.Current);
    }

    [Theory]
    [InlineData("docs/storage.md#Limits", "anchor not found")]
    [InlineData("docs/gone.md#Quotas", "not found; not in a git repository")]
    public async Task UnresolvedRef_HasTheReasonAndNoText(string sourceRef, string reason)
    {
        _ws.WriteDoc("docs/gone.md", "# Gone\n\n## Quotas\n\nOld.\n");
        _ws.WriteTest("storage", "TC-101", [sourceRef, "docs/storage.md#Sharing"]);
        await _ws.InitBaselineAsync();
        File.Delete(_ws.PathOf("docs/gone.md"));

        var section = Section(await BuildAsync("TC-101"), sourceRef);

        Assert.Equal(SectionState.Removed, section.State);
        Assert.Equal(reason, section.Reason);
        Assert.Null(section.Current);
    }

    [GitFact]
    public async Task RenamedFile_IsRelinkWithTheTarget()
    {
        _ws.InitGit();
        _ws.WriteDoc("docs/storage.md", StorageDoc());
        _ws.Commit("one");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Move("docs/storage.md", "docs/guides/storage.md");
        _ws.Commit("move");

        var section = Section(await BuildAsync("TC-101"), "docs/storage.md#Quotas");

        Assert.Equal(SectionState.Relink, section.State);
        Assert.Equal("renamed to docs/guides/storage.md", section.Reason);
        Assert.Null(section.Current);
    }

    [Fact]
    public async Task Criteria_OnlyTheLinkedOnes_WithTheirState()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], ["AC-2", "AC-1", "AC-9"]);
        await _ws.InitBaselineAsync();
        _ws.WriteCriteria("storage.criteria.yaml",
            ("AC-1", "Each user MUST get 45GB"), ("AC-2", "Files MAY be shared by link"), ("AC-OTHER", "Unrelated rule"));

        var ctx = await BuildAsync("TC-101");

        Assert.Equal(["AC-1", "AC-2", "AC-9"], ctx.Criteria.Select(c => c.Id));
        Assert.Equal(LinkedCriterionState.ChangedSinceBaseline, ctx.Criteria[0].State);
        Assert.Equal("Each user MUST get 45GB", ctx.Criteria[0].Text);
        Assert.Equal(LinkedCriterionState.Unchanged, ctx.Criteria[1].State);
        Assert.Equal(LinkedCriterionState.Removed, ctx.Criteria[2].State);
        Assert.Null(ctx.Criteria[2].Text);
        Assert.DoesNotContain(ctx.Criteria, c => c.Id == "AC-OTHER");
    }

    [Fact]
    public async Task Criteria_NeverBaselined_IsNotBaselined_NoneLinked_IsEmpty()
    {
        // Spec 089 (F4): was Unchanged on 088 — a criterion with nothing to compare against is NotBaselined.
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"], ["AC-2"]); // no baseline: AC-2 never baselined

        Assert.Empty((await BuildAsync("TC-101")).Criteria);
        var criterion = Assert.Single((await BuildAsync("TC-102")).Criteria);
        Assert.Equal(LinkedCriterionState.NotBaselined, criterion.State);
    }

    [Fact]
    public async Task Criteria_AnEntryWithoutTheCriterion_IsNotBaselined_AndOutranksUnchangedAcrossTests()
    {
        // Spec 089 (F4): TC-101 baselined with AC-1; TC-102 baselined without it, then linked.
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], ["AC-1"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"], ["AC-1"]);

        Assert.Equal(LinkedCriterionState.Unchanged, Assert.Single((await BuildAsync("TC-101")).Criteria).State);
        Assert.Equal(LinkedCriterionState.NotBaselined, Assert.Single((await BuildAsync("TC-102")).Criteria).State);
        Assert.Equal(LinkedCriterionState.NotBaselined, Assert.Single((await BuildAsync("TC-101", "TC-102")).Criteria).State);

        _ws.WriteCriteria("storage.criteria.yaml",
            ("AC-1", "Each user MUST get 45GB"), ("AC-2", "Files MAY be shared by link"), ("AC-OTHER", "Unrelated rule"));

        Assert.Equal(LinkedCriterionState.ChangedSinceBaseline,
            Assert.Single((await BuildAsync("TC-101", "TC-102")).Criteria).State);
    }

    [Fact]
    public async Task SharedRef_SameFingerprint_IsOneBlock_DifferentFingerprints_AreTwo()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        Edit("30GB", "45GB");

        var shared = Assert.Single((await BuildAsync("TC-101", "TC-102")).Sections);
        Assert.Equal(["TC-101", "TC-102"], shared.Tests);

        await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-102"); // TC-102 re-baselined at 45GB
        var split = await BuildAsync("TC-101", "TC-102");
        Assert.Equal(2, split.Sections.Count);
        Assert.Equal(SectionState.Changed, split.Sections[0].State);
        Assert.Equal(SectionState.Unchanged, split.Sections[1].State);
        Assert.Equal(["S1"], split.Tests[0].SectionLabels);
        Assert.Equal(["S2"], split.Tests[1].SectionLabels);
    }

    [Fact]
    public async Task NothingToReconcile_OnlyWhenNoRefResolvesAndNoLinkedCriterionExists()
    {
        _ws.WriteDoc("docs/gone.md", "# Gone\n\n## Quotas\n\nOld.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/gone.md#Quotas"], ["AC-9"]);
        _ws.WriteTest("storage", "TC-102", ["docs/gone.md#Quotas"], ["AC-1"]);
        _ws.WriteTest("storage", "TC-103", ["docs/gone.md#Quotas", "docs/storage.md#Sharing"]);
        File.Delete(_ws.PathOf("docs/gone.md"));

        var ctx = await BuildAsync("TC-101", "TC-102", "TC-103");

        Assert.Equal(["TC-101"], ctx.NothingToReconcile);
    }
}
