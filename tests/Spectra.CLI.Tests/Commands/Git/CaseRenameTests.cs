using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 087 T4 (F4, FR-012–FR-014), on a case-insensitive file system: a ref whose casing differs from git's record of
/// the file is still relinked when the file is renamed (with git's casing), two history paths differing only in case
/// are ambiguous, and the history bootstrap follows a rename back the same way.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CaseRenameTests : IDisposable
{
    private static readonly DateTimeOffset Day1 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day3 = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly BaselineTestWorkspace _ws = new();

    public CaseRenameTests()
    {
        if (GitAvailability.IsAvailable && !OperatingSystem.IsLinux()) _ws.InitGit();
    }

    public void Dispose() => _ws.Dispose();

    [CaseInsensitiveGitFact]
    public async Task ADifferentlyCasedRef_IsRelinked_WithGitsCasing()
    {
        _ws.WriteDoc("docs/store-manager/setup.md", StorageDoc());
        _ws.Commit("one");
        _ws.WriteTest("storage", "TC-101", ["docs/Store-Manager/Setup.md#Quotas"]); // resolves on this file system
        await _ws.InitBaselineAsync();
        _ws.Commit("baseline");
        _ws.Move("docs/store-manager/setup.md", "docs/guides/Setup-Guide.md");
        _ws.Commit("rename");

        var triage = await _ws.TriageAsync();

        var group = Group(triage, "TC-101");
        Assert.Equal("RELINK", group.GetProperty("bucket").GetString());
        Assert.Equal(["renamed to docs/guides/Setup-Guide.md"], Strings(group.GetProperty("why")));
        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage");
        Assert.Equal("docs/guides/Setup-Guide.md#Quotas", RelinkedTo(relink, "TC-101"));
        Assert.Contains("docs/guides/Setup-Guide.md#Quotas", File.ReadAllText(_ws.PathOf("test-cases/storage/TC-101.md")));
    }

    [CaseInsensitiveGitFact]
    public async Task TwoHistoryPathsDifferingOnlyInCase_AreAmbiguous()
    {
        _ws.WriteDoc("docs/Setup.md", StorageDoc("10GB"));
        _ws.Commit("Setup");
        _ws.WriteTest("storage", "TC-101", ["docs/SETUP.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("baseline");
        _ws.Move("docs/Setup.md", "docs/x.md");
        _ws.Commit("Setup → x");
        _ws.WriteDoc("docs/setup.md", StorageDoc("20GB").Replace("Background line", "Other line"));
        _ws.Commit("setup");
        _ws.Move("docs/setup.md", "docs/y.md");
        _ws.Commit("setup → y");

        var triage = await _ws.TriageAsync();

        Assert.Equal("not found; ambiguous rename", Removed(triage, "TC-101").GetProperty("reason").GetString());
    }

    [CaseInsensitiveGitFact]
    public async Task Bootstrap_FollowsTheRenameBack_WhenTheRefsCasingDiffers()
    {
        _ws.WriteDoc("docs/store-manager/setup.md", StorageDoc());
        _ws.Commit("one", Day1);
        _ws.Move("docs/store-manager/setup.md", "docs/guides/Setup-Guide.md");
        _ws.Edit("docs/guides/Setup-Guide.md", "10GB", "50GB");
        _ws.Commit("rename and change", Day3);
        _ws.WriteTest("storage", "TC-101", ["docs/Guides/setup-guide.md#Quotas"]); // resolves on this file system

        var result = await _ws.InitBaselineAsync("storage", "--from-date", "2026-03-15");

        Assert.Empty(result.GetProperty("suites")[0].GetProperty("unbaselined").EnumerateArray());
        var group = Group(await _ws.TriageAsync(), "TC-101");
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());
        Assert.Contains("numbers: 10gb→50gb", Strings(group.GetProperty("why")));
    }
}
