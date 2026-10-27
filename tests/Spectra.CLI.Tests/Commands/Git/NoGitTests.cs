using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T6 (FR-003, FR-021): without git everything behaves as under 085 — no RELINK, a missing file's REMOVED
/// reason says <c>not found; no git</c>, <c>relink-sources</c> changes nothing, and a history bootstrap falls back to
/// current content with a warning. No real git is needed: the runner reports git as not installed.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class NoGitTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public NoGitTests()
    {
        GitAccess.RunnerOverride = FakeGitRunner.NotInstalled();
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteDoc("docs/b.md", Doc("B", ("Limits", "Ten files.")));
    }

    public void Dispose()
    {
        GitAccess.RunnerOverride = null;
        _ws.Dispose();
    }

    [Fact]
    public async Task Triage_MissingFile_IsRemoved_NoGit_NeverRelink()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        File.Delete(_ws.PathOf("docs/a.md"));

        var triage = await _ws.TriageAsync();

        Assert.Equal("REMOVED", Group(triage, "TC-101").GetProperty("bucket").GetString());
        Assert.Equal("not found; no git", Removed(triage, "TC-101").GetProperty("reason").GetString());
        Assert.Equal(0, BaselineTestWorkspace.Count(triage, "storage", "relink"));
        var (_, human, _) = await _ws.RunAsync("ai", "update-triage", "--suite", "storage", "--dry-run");
        Assert.DoesNotContain("RELINK", human);
    }

    [Fact]
    public async Task RelinkSources_ReportsNoGit_ChangesNothing()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/b.md#Limits"]);
        await _ws.InitBaselineAsync();
        File.Delete(_ws.PathOf("docs/a.md"));
        var before = _ws.Snapshot();

        var result = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage");

        Assert.Empty(result.GetProperty("relinked").EnumerateArray());
        var notRelinked = Assert.Single(result.GetProperty("not_relinked").EnumerateArray());
        Assert.Equal("docs/a.md#Quotas", notRelinked.GetProperty("ref").GetString());
        Assert.Equal("no git", notRelinked.GetProperty("reason").GetString());
        Assert.Equal(before, _ws.Snapshot(".spectra/baseline.lock"));
    }

    [Fact]
    public async Task FromDate_UsesCurrentContent_WithAWarning()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);

        var result = await _ws.InitBaselineAsync("storage", "--from-date", "2026-03-15");

        Assert.Equal(1, result.GetProperty("suites")[0].GetProperty("created").GetInt32());
        var warning = Assert.Single(result.GetProperty("warnings").EnumerateArray()).GetString();
        Assert.Contains("used current content", warning);
        Assert.Contains("docs/a.md", warning);
        Assert.Equal(1, BaselineTestWorkspace.Count(await _ws.TriageAsync(), "storage", "unchanged"));
    }
}
