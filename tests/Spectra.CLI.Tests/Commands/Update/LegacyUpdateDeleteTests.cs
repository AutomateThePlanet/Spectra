using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Update;

/// <summary>
/// Spec 090 T10 (FR-C3): legacy <c>spectra ai update … --delete-orphaned</c> deletes an orphaned test through the shared
/// delete service — index, scalar <c>depends_on</c>, baseline entry, staging and verdict files — never with a bare file
/// delete. The legacy classifier marks a test Orphaned when none of its source refs resolve
/// (<c>TestClassifier</c>: "no source content or source not found").
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class LegacyUpdateDeleteTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task AnOrphanedTest_IsDeletedThroughTheDeleteService()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteDoc("docs/gone.md", "# Gone\n\n## Old\n\nAn old feature.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/gone.md#Old"]);
        // The dependent lives in another suite: legacy update classifies only 'storage' (its word-overlap rule would
        // mark a thin neighbour Orphaned too), while the delete service cleans depends_on across suites.
        _ws.WriteTest("other", "TC-102", ["docs/storage.md#Quotas"]);
        var tc102 = _ws.TestPath("other", "TC-102");
        File.WriteAllText(tc102, File.ReadAllText(tc102).Replace("priority: high\n", "priority: high\ndepends_on: TC-101\n"));
        _ws.RebuildIndex("other");
        await _ws.InitBaselineAsync();
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));
        _ws.WriteVerdict("TC-101", "grounded");
        File.Delete(_ws.PathOf("docs/gone.md"));

        var (exit, output, error) = await _ws.RunAsync(
            "ai", "update", "storage", "--no-interaction", "--no-review", "--delete-orphaned");

        Assert.True(exit == 0, $"{output}\n{error}");
        Assert.False(File.Exists(_ws.TestPath("storage", "TC-101")));
        Assert.DoesNotContain("TC-101", File.ReadAllText(_ws.PathOf("test-cases/storage/_index.json")));
        Assert.False(_ws.BaselineJson("storage").GetProperty("tests").TryGetProperty("TC-101", out _));
        Assert.Empty(_ws.StagedFiles("storage"));
        Assert.False(File.Exists(_ws.VerdictPath("TC-101")));
        Assert.DoesNotContain("depends_on", File.ReadAllText(tc102));
    }
}
