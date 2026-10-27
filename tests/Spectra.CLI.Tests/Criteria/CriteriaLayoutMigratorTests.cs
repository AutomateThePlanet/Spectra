using Spectra.CLI.Criteria;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Criteria;

/// <summary>Spec 079 FR-016: the one-time, deterministic, idempotent per-doc migration.</summary>
public sealed class CriteriaLayoutMigratorTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private async Task<MigrationReport> MigrateAsync(bool dryRun = false) =>
        await CriteriaLayoutMigrator.MigrateAsync(
            await CriteriaStore.LoadAsync(SpectraConfig.Default, _ws.Root, default), dryRun, default);

    [Fact]
    public async Task Migrate_MovesFiles_NormalisesDocs_PreservesIds_SeedsMarks()
    {
        OldLayoutCriteria.Seed(_ws);

        var report = await MigrateAsync();

        Assert.False(report.AlreadyMigrated);
        Assert.Equal(["docs/checkout.criteria.yaml", "docs/login.criteria.yaml"], report.Moved.Select(m => m.ToFile));
        Assert.Equal(1, report.NormalizedSourceDocs);
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/checkout.criteria.yaml")));
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/login.criteria.yaml")));

        var checkout = await _ws.ReadCriteriaAsync("docs/checkout.criteria.yaml");
        Assert.Equal(["AC-CHECKOUT-001", "AC-CHECKOUT-007"], checkout.Select(c => c.Id));
        Assert.All(checkout, c => Assert.Equal("docs/checkout.md", c.SourceDoc));
        Assert.Equal("AC-LOGIN-002", (await _ws.ReadCriteriaAsync("docs/login.criteria.yaml"))[0].Id);

        var index = await _ws.ReadIndexAsync();
        Assert.Equal(["docs/checkout.criteria.yaml", "docs/login.criteria.yaml", "imported/jira.criteria.yaml"],
            index.Sources.Select(s => s.File));
        Assert.Equal("docs/checkout.md", index.Sources[0].SourceDoc);
        Assert.Equal(7, index.IdHighWaterMarks!["AC-CHECKOUT"]);
        Assert.Equal(2, index.IdHighWaterMarks["AC-LOGIN"]);
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/imported/jira.criteria.yaml"))); // import untouched
    }

    [Fact]
    public async Task SecondRun_ChangesNothing()
    {
        OldLayoutCriteria.Seed(_ws);
        await MigrateAsync();
        var before = _ws.Snapshot();

        var report = await MigrateAsync();

        Assert.True(report.AlreadyMigrated);
        Assert.Empty(report.Moved);
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task SharedFile_IsReportedAsACollision_AndLeftUntouched()
    {
        OldLayoutCriteria.SeedCollision(_ws);
        var before = _ws.Snapshot();

        var report = await MigrateAsync();

        var collision = Assert.Single(report.Collisions);
        Assert.Equal("login.criteria.yaml", collision.File);
        Assert.Equal(["docs/a/login.md", "docs/b/login.md"], collision.Docs);
        Assert.Contains("Re-extract both docs", collision.Recommendation);
        Assert.Equal(before["docs/criteria/login.criteria.yaml"], _ws.Snapshot()["docs/criteria/login.criteria.yaml"]);
    }

    [Fact]
    public async Task DryRun_WritesNothing()
    {
        OldLayoutCriteria.Seed(_ws);
        var before = _ws.Snapshot();

        var report = await MigrateAsync(dryRun: true);

        Assert.Equal(2, report.Moved.Count);
        Assert.Equal(before, _ws.Snapshot());
    }
}
