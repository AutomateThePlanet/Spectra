using System.Security.Cryptography;
using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Delete;

/// <summary>
/// Spec 090 T8/T9 (FR-C1, FR-C2): deleting a test changes only the scalar <c>depends_on</c> of its dependents (through
/// the lossless writer), never body lines or <c>criteria:</c>; leaves list-form dependents untouched with a warning;
/// and removes the deleted test's baseline entry, staging files and verdict files.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SafeDeleteTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static string Markdown(string id, string extraFrontmatter = "", string extraBody = "", string newline = "\n") =>
        ("---\n" +
         $"id: {id}\n" +
         "priority: high\n" +
         extraFrontmatter +
         $"source_refs:\n  - \"{Quotas}\"\n" +
         "---\n\n" +
         $"# A test {id}\n\n" +
         "## Steps\n\n1. Do the thing\n" + extraBody + "\n" +
         "## Expected Result\n\nIt works\n").Replace("\n", newline);

    private string Write(string id, string content)
    {
        var path = _ws.TestPath("storage", id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>TC-101 (to delete) and its neighbours; baselined.</summary>
    private async Task ArrangeAsync(string newline = "\n")
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 30GB"));
        Write("TC-101", Markdown("TC-101"));
        Write("TC-102", Markdown("TC-102", "depends_on: TC-101\n", newline: newline));
        Write("TC-103", Markdown("TC-103", extraBody: "- TC-101\n").Replace($"source_refs:", "criteria:\n  - AC-1\nsource_refs:"));
        Write("TC-104", Markdown("TC-104", "depends_on:\n  - TC-101\n"));
        Write("TC-105", Markdown("TC-105", "depends_on: [TC-101, TC-102]\n"));
        Write("TC-106", Markdown("TC-106", "depends_on: TC-101\n", extraBody: "id: TC-999\n"));
        _ws.RebuildIndex("storage");
        await _ws.InitBaselineAsync();
    }

    private Task<(int Exit, string Out, string Err)> DeleteAsync(params string[] extra) =>
        _ws.RunAsync(["delete", "TC-101", "--force", "--no-interaction", .. extra, "--output-format", "json"]);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private string Read(string id) => File.ReadAllText(_ws.TestPath("storage", id));

    private Dictionary<string, string> HashTree()
    {
        return Directory.EnumerateFiles(_ws.Root, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".spectra-result.json", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(_ws.Root, f),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task AScalarDependent_LosesOnlyItsDependsOnLine(string newline)
    {
        await ArrangeAsync(newline);
        var before = Read("TC-102");

        var (exit, output, error) = await DeleteAsync();

        Assert.True(exit == 0, error);
        Assert.Equal(before.Replace("depends_on: TC-101" + newline, ""), Read("TC-102"));
        var cleanup = BaselineTestWorkspace.Json(output).GetProperty("dependency_cleanup").EnumerateArray()
            .Select(c => (c.GetProperty("test_id").GetString(), c.GetProperty("removed_dep").GetString())).ToList();
        Assert.Contains(("TC-102", "TC-101"), cleanup);
        Assert.Contains(("TC-106", "TC-101"), cleanup); // its frontmatter id, never the body's "id: TC-999"
        Assert.DoesNotContain(cleanup, c => c.Item1 == "TC-999");
    }

    [Fact]
    public async Task ACommentAboveDependsOn_GoesWithTheKey()
    {
        await ArrangeAsync();
        Write("TC-102", Markdown("TC-102", "# needs the quota test\ndepends_on: TC-101\n"));
        var before = Read("TC-102");

        var (exit, _, error) = await DeleteAsync();

        Assert.True(exit == 0, error);
        Assert.Equal(before.Replace("# needs the quota test\ndepends_on: TC-101\n", ""), Read("TC-102"));
    }

    [Fact]
    public async Task BodyLinesCriteriaAndListForms_AreNeverTouched_ListFormsAreWarned()
    {
        await ArrangeAsync();
        var before = new[] { "TC-103", "TC-104", "TC-105" }.ToDictionary(id => id, Read);

        var (exit, output, error) = await DeleteAsync();

        Assert.True(exit == 0, error);
        Assert.All(before, kv => Assert.Equal(kv.Value, Read(kv.Key)));
        var warnings = Strings(BaselineTestWorkspace.Json(output).GetProperty("warnings"));
        Assert.Contains(warnings, w => w.Contains("TC-104") && w.Contains("TC-101") && w.Contains("depends_on is a list"));
        Assert.Contains(warnings, w => w.Contains("TC-105") && w.Contains("TC-101") && w.Contains("depends_on is a list"));
        Assert.DoesNotContain(warnings, w => w.Contains("TC-103"));
    }

    [Fact]
    public async Task TheBaselineEntry_StagingAndVerdictFiles_Go_SnapshotsStay()
    {
        await ArrangeAsync();
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));
        _ws.StageRaw("storage", "TC-102", "[]");
        File.WriteAllText(_ws.PathOf(".spectra/updates/storage/failed-TC-101.json"), "[]");
        _ws.StageRepairRaw("storage", "TC-101", "[]");
        _ws.WriteVerdict("TC-101", "grounded");
        _ws.WriteVerdict("TC-102", "grounded");
        File.WriteAllText(_ws.VerdictRecordPath("TC-101"), "{}");
        var baselineBefore = _ws.BaselineJson("storage").GetProperty("tests");
        var snapshots = _ws.SnapshotFiles();

        var (exit, output, error) = await DeleteAsync();

        Assert.True(exit == 0, error);
        var tests = _ws.BaselineJson("storage").GetProperty("tests");
        Assert.False(tests.TryGetProperty("TC-101", out _));
        foreach (var entry in baselineBefore.EnumerateObject().Where(e => e.Name != "TC-101"))
        {
            Assert.Equal(entry.Value.GetRawText(), tests.GetProperty(entry.Name).GetRawText());
        }

        Assert.Equal(["updated-TC-102.json"], _ws.StagedFiles("storage"));
        Assert.Empty(_ws.RepairFiles("storage"));
        Assert.False(File.Exists(_ws.VerdictPath("TC-101")));
        Assert.False(File.Exists(_ws.VerdictRecordPath("TC-101")));
        Assert.True(File.Exists(_ws.VerdictPath("TC-102")));
        Assert.Equal(snapshots, _ws.SnapshotFiles());

        var json = BaselineTestWorkspace.Json(output);
        Assert.Equal(["storage/TC-101"], Strings(json.GetProperty("baseline_entries_removed")));
        Assert.Equal(3, json.GetProperty("staging_files_removed").GetArrayLength());
        Assert.Equal(2, json.GetProperty("verdict_files_removed").GetArrayLength());
    }

    [Fact]
    public async Task ADryRun_ListsEverything_AndChangesNothing()
    {
        await ArrangeAsync();
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));
        _ws.WriteVerdict("TC-101", "grounded");
        var before = HashTree();

        var (exit, output, error) = await DeleteAsync("--dry-run");

        Assert.True(exit == 0, error);
        Assert.Equal(before, HashTree());
        var json = BaselineTestWorkspace.Json(output);
        Assert.True(json.GetProperty("dry_run").GetBoolean());
        Assert.Equal(["storage/TC-101"], Strings(json.GetProperty("baseline_entries_removed")));
        Assert.Equal([".spectra/updates/storage/updated-TC-101.json"], Strings(json.GetProperty("staging_files_removed")));
        Assert.Equal([".spectra/verdicts/critic-verdict-TC-101.json"], Strings(json.GetProperty("verdict_files_removed")));
        Assert.Equal(2, json.GetProperty("warnings").GetArrayLength());
        Assert.Equal(2, json.GetProperty("dependency_cleanup").GetArrayLength());
    }

    [Fact]
    public async Task ATriageGroupNamingTheDeletedTest_IsStale()
    {
        await ArrangeAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-101").GetProperty("id").GetString()!;
        var (deleteExit, _, deleteError) = await DeleteAsync();
        Assert.True(deleteExit == 0, deleteError);

        var (exit, _, error) = await _ws.RunAsync("ai", "update-ack", "--suite", "storage", "--group", group, "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Equal("TRIAGE_STALE", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task RealInitWorkspace_DeleteRemovesTheBaselineEntry_AndCleansTheDependent()
    {
        using var ws = new BaselineTestWorkspace(writeConfig: false);
        await ws.InitWithRealTemplateAsync();
        ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        var (ingestExit, _, ingestError) = await ws.RunAsync("ai", "ingest-tests", "storage", "--from",
            ws.GeneratedPayload(("TC-101", [Quotas], []), ("TC-102", [Quotas], [])), "--output-format", "json");
        Assert.True(ingestExit == 0, ingestError);
        var tc102 = ws.TestPath("storage", "TC-102");
        File.WriteAllText(tc102, File.ReadAllText(tc102).Replace("priority: high\n", "priority: high\ndepends_on: TC-101\n"));
        var before = File.ReadAllText(tc102);

        var (exit, _, error) = await ws.RunAsync("delete", "TC-101", "--force", "--no-interaction", "--output-format", "json");

        Assert.True(exit == 0, error);
        Assert.False(ws.BaselineJson("storage").GetProperty("tests").TryGetProperty("TC-101", out _));
        Assert.Equal(before.Replace("depends_on: TC-101\n", ""), File.ReadAllText(tc102));
    }
}
