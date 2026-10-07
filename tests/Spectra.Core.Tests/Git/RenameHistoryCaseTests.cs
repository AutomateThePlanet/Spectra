using Spectra.Core.Git;

namespace Spectra.Core.Tests.Git;

/// <summary>
/// Spec 087 T4 (F4, FR-012/FR-013): where the file system ignores case (Windows, macOS), a path is looked up in the
/// rename history ignoring case — one distinct match gives git's casing, more than one is ambiguous; Linux stays exact.
/// The comparer is passed explicitly, so both rules are tested on every OS.
/// </summary>
public class RenameHistoryCaseTests
{
    private static RenameHistory History(StringComparer comparer, params (string Old, string New)[] newestFirst) =>
        new(newestFirst.Select((r, i) => new GitRename($"c{i}", 100 - i, r.Old, r.New, 100)).ToList(), 90, shallow: false, comparer);

    private static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer Exact = StringComparer.Ordinal;

    [Fact]
    public void IgnoringCase_ADifferentlyCasedPath_FindsGitsRename_WithGitsTargetCasing()
    {
        var history = History(IgnoreCase, ("docs/store-manager/setup.md", "docs/guides/Setup-Guide.md"));

        var (target, reason) = history.ForwardCandidate("docs/Store-Manager/Setup.md", p => p == "docs/guides/Setup-Guide.md");

        Assert.Null(reason);
        Assert.Equal("docs/guides/Setup-Guide.md", target);
    }

    [Fact]
    public void IgnoringCase_TwoHistoryPathsDifferingOnlyInCase_AreAmbiguous()
    {
        var history = History(IgnoreCase, ("docs/setup.md", "docs/y.md"), ("docs/Setup.md", "docs/x.md"));

        Assert.Equal((null, RenameHistory.Ambiguous), history.ForwardCandidate("docs/SETUP.md", _ => true));
    }

    [Fact]
    public void IgnoringCase_AnExactAndAnOtherCaseMatch_AreAmbiguous()
    {
        var history = History(IgnoreCase, ("docs/setup.md", "docs/y.md"), ("docs/Setup.md", "docs/x.md"));

        Assert.Equal((null, RenameHistory.Ambiguous), history.ForwardCandidate("docs/Setup.md", _ => true));
    }

    [Fact]
    public void IgnoringCase_AChainStepRecordedWithOtherCasing_IsFollowed()
    {
        var history = History(IgnoreCase, ("Docs/B.md", "docs/c.md"), ("docs/a.md", "docs/b.md"));

        Assert.Equal(("docs/c.md", (string?)null), history.ForwardCandidate("docs/a.md", p => p == "docs/c.md"));
    }

    [Fact]
    public void Exact_ADifferentlyCasedPath_FindsNothing()
    {
        var history = History(Exact, ("docs/store-manager/setup.md", "docs/guides/setup.md"));

        Assert.Equal((null, "no rename found at 90% similarity"), history.ForwardCandidate("docs/Store-Manager/Setup.md", _ => true));
    }

    [Fact]
    public void Exact_TwoCaseVariants_AreTwoUnrelatedPaths()
    {
        var history = History(Exact, ("docs/setup.md", "docs/y.md"), ("docs/Setup.md", "docs/x.md"));

        Assert.Equal(("docs/x.md", (string?)null), history.ForwardCandidate("docs/Setup.md", _ => true));
    }

    private static readonly GitRename[] Since =
    [
        new("2", 2, "docs/b.md", "docs/Guides/C.md", 100),
        new("1", 1, "docs/a.md", "docs/b.md", 100)
    ];

    [Fact]
    public void PathAt_IgnoringCase_MapsADifferentlyCasedCurrentPathToGitsOldPath() =>
        Assert.Equal(("docs/a.md", (string?)null), RenameHistory.PathAt(Since, "docs/guides/c.md", IgnoreCase));

    [Fact]
    public void PathAt_Exact_LeavesADifferentlyCasedPathAlone() =>
        Assert.Equal(("docs/guides/c.md", (string?)null), RenameHistory.PathAt(Since, "docs/guides/c.md", Exact));

    [Fact]
    public void PathAt_IgnoringCase_TwoCaseVariantTargets_AreAmbiguous()
    {
        GitRename[] since = [new("2", 2, "docs/x.md", "docs/Setup.md", 100), new("1", 1, "docs/y.md", "docs/setup.md", 100)];

        Assert.Equal((null, RenameHistory.Ambiguous), RenameHistory.PathAt(since, "docs/SETUP.md", IgnoreCase));
        Assert.Equal(("docs/x.md", (string?)null), RenameHistory.PathAt(since, "docs/Setup.md", Exact));
    }
}
