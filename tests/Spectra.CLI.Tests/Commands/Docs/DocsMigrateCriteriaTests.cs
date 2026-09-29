using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Docs;

/// <summary>Spec 079 FR-016a: <c>spectra docs migrate-criteria [--dry-run]</c>.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class DocsMigrateCriteriaTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task Json_ReportsMovesAndSeeds_ThenAlreadyMigrated()
    {
        OldLayoutCriteria.Seed(_ws);

        var (exit, output, err) = await _ws.RunAsync("docs", "migrate-criteria", "--output-format", "json");

        Assert.True(exit == 0, err);
        var result = CriteriaTestWorkspace.Json(output);
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(2, result.GetProperty("moved").GetArrayLength());
        Assert.Equal(7, result.GetProperty("high_water_marks_seeded").GetProperty("AC-CHECKOUT").GetInt32());
        Assert.False(result.GetProperty("already_migrated").GetBoolean());

        var (_, again, _) = await _ws.RunAsync("docs", "migrate-criteria", "--output-format", "json");
        Assert.True(CriteriaTestWorkspace.Json(again).GetProperty("already_migrated").GetBoolean());
    }

    [Fact]
    public async Task Human_ListsMovesAndCollisions()
    {
        OldLayoutCriteria.SeedCollision(_ws);

        var (exit, output, _) = await _ws.RunAsync("docs", "migrate-criteria", "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains("Collision: login.criteria.yaml is shared by docs/a/login.md and docs/b/login.md", output);
        Assert.Contains("Dry run", output);
    }
}
