using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 089 T1 (F1, FR-001–FR-004): update edits and critic repairs have one staging folder each. <c>--all</c> reads only
/// <c>.spectra/updates/&lt;suite&gt;/</c>; <c>--all --keep-baseline</c> reads only <c>.spectra/repairs/&lt;suite&gt;/</c>, so
/// neither ever ingests the other's leftovers in the wrong baseline mode. On 6ea7520 rows (b), (c), (d) and (f) fail; row
/// (a) passes there too (the update batch never read a repairs folder) and guards the direction F1 adds.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class StagingFolderSplitTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";
    private const string Malformed = "[\n  { \"id\": \"TC-102\", \"title\": }\n]\n";

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    /// <summary>TC-101..TC-103 on Quotas, baselined; then 30GB → 45GB, so each has a pending LOAD change.</summary>
    private async Task PendingLoadAsync()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            _ws.WriteTest("storage", id, [Quotas]);
        }

        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
    }

    private Spectra.Core.Models.TestCase Edited(string id, string expected = "Each user gets 45GB") =>
        _ws.Load("storage", id) with { ExpectedResult = expected };

    private static JsonElement Batch(string stdout) => BaselineTestWorkspace.Json(stdout);

    private static string[] ResultTests(JsonElement batch) =>
        batch.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("test").GetString()!).ToArray();

    private static JsonElement Result(JsonElement batch, string id) =>
        batch.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("test").GetString() == id);

    private async Task<string?> BucketAsync(string id)
    {
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync("storage", "--dry-run"), id);
        return group.ValueKind == JsonValueKind.Undefined ? null : group.GetProperty("bucket").GetString();
    }

    private string TestFile(string id) => File.ReadAllText(_ws.PathOf($"test-cases/storage/{id}.md"));

    [Fact]
    public async Task A_TheUpdateBatch_NeverReadsAKeptRepairFile()
    {
        await PendingLoadAsync();
        _ws.StageRepair("storage", "TC-101", Edited("TC-101"));
        var fileBefore = TestFile("TC-101");
        var baselineBefore = _ws.ReadBaseline("storage");

        var (_, output, error) = await _ws.IngestAllAsync();

        Assert.DoesNotContain("TC-101", ResultTests(Batch(output)));
        Assert.Equal(fileBefore, TestFile("TC-101"));
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.Equal(["repaired-TC-101.json"], _ws.RepairFiles("storage"));
        Assert.Equal("LOAD", await BucketAsync("TC-101"));
        Assert.DoesNotContain("repaired-", error);
    }

    [Fact]
    public async Task B_TheRepairBatch_NeverReadsAKeptUpdateFile()
    {
        await PendingLoadAsync();
        _ws.StageRaw("storage", "TC-102", Malformed);

        var (exit, output, _) = await _ws.IngestAllAsync("storage", "--keep-baseline");

        Assert.Equal(0, exit);
        Assert.Empty(ResultTests(Batch(output)));
        Assert.Equal(["updated-TC-102.json"], _ws.StagedFiles("storage"));
        Assert.Equal(Malformed, File.ReadAllText(_ws.PathOf(".spectra/updates/storage/updated-TC-102.json")));
    }

    [Fact]
    public async Task C_TheRepairBatch_IngestsItsOwnFolder_KeepsTheBaseline_AndDeletesTheFile()
    {
        await PendingLoadAsync();
        _ws.StageRepair("storage", "TC-101", Edited("TC-101", "Repaired expectation"));

        var (exit, output, error) = await _ws.IngestAllAsync("storage", "--keep-baseline");

        Assert.True(exit == 0, error);
        var batch = Batch(output);
        var result = Result(batch, "TC-101");
        Assert.Equal("written", result.GetProperty("outcome").GetString());
        Assert.Equal(".spectra/repairs/storage/repaired-TC-101.json", result.GetProperty("file").GetString());
        Assert.Equal("keep", batch.GetProperty("baseline").GetProperty("mode").GetString());
        Assert.Contains("Repaired expectation", TestFile("TC-101"));
        Assert.Empty(_ws.RepairFiles("storage"));
        Assert.Equal("LOAD", await BucketAsync("TC-101")); // a repair never marks the doc change as reviewed (085)
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task D_BackToBack_WithLeftoversInBothFolders_EachFlowIngestsOnlyItsOwnFiles(bool repairsFirst)
    {
        await PendingLoadAsync();
        await AssertBackToBackAsync(repairsFirst);
    }

    [Fact]
    public async Task E_BackToBack_OnARealInitWorkspace()
    {
        using var ws = new BaselineTestWorkspace(writeConfig: false);
        await ws.InitWithRealTemplateAsync();
        ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        var (exit, _, error) = await ws.RunAsync("ai", "ingest-tests", "storage", "--from",
            ws.GeneratedPayload(("TC-101", [Quotas], []), ("TC-102", [Quotas], []), ("TC-103", [Quotas], [])),
            "--output-format", "json");
        Assert.True(exit == 0, error);
        ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");

        ws.StageRaw("storage", "TC-102", Malformed);
        ws.StageEdit("storage", "TC-103", ws.Load("storage", "TC-103") with { ExpectedResult = "Each user gets 45GB" });
        ws.StageRepairRaw("storage", "TC-102", Malformed);
        ws.StageRepair("storage", "TC-101", ws.Load("storage", "TC-101") with { ExpectedResult = "Repaired expectation" });

        var repairs = BaselineTestWorkspace.Json((await ws.IngestAllAsync("storage", "--keep-baseline")).Out);
        Assert.Equal(["TC-101", "TC-102"], ResultTests(repairs));
        Assert.All(repairs.GetProperty("results").EnumerateArray(),
            r => Assert.StartsWith(".spectra/repairs/", r.GetProperty("file").GetString()));
        Assert.Equal(["updated-TC-102.json", "updated-TC-103.json"], ws.StagedFiles("storage"));

        var updates = BaselineTestWorkspace.Json((await ws.IngestAllAsync()).Out);
        Assert.Equal(["TC-102", "TC-103"], ResultTests(updates));
        Assert.All(updates.GetProperty("results").EnumerateArray(),
            r => Assert.StartsWith(".spectra/updates/", r.GetProperty("file").GetString()));
        Assert.Equal(["failed-TC-102.json"], ws.RepairFiles("storage")); // Spec 090 (FR-B1): a failed file is kept as failed-<id>.json
    }

    [Fact]
    public async Task F_APre089RepairLeftoverInTheUpdateFolder_IsReadOnlyByTheUpdateBatch()
    {
        // FR-025 / documented edge case: a pre-089 repair loop staged repairs in .spectra/updates/. 089 can't tell them
        // from update edits; the repair batch never reads them, the update batch ingests them in Rebuild mode.
        await PendingLoadAsync();
        _ws.StageEdit("storage", "TC-101", Edited("TC-101", "A pre-089 repair"));

        var repairs = Batch((await _ws.IngestAllAsync("storage", "--keep-baseline")).Out);

        Assert.Empty(ResultTests(repairs));
        Assert.Equal(["updated-TC-101.json"], _ws.StagedFiles("storage"));
        Assert.Equal("LOAD", await BucketAsync("TC-101"));

        var updates = Batch((await _ws.IngestAllAsync()).Out);

        Assert.Equal("written", Result(updates, "TC-101").GetProperty("outcome").GetString());
        Assert.Equal("rebuild", updates.GetProperty("baseline").GetProperty("mode").GetString());
        Assert.Null(await BucketAsync("TC-101")); // rebuilt: the pending change now counts as reviewed
    }

    private async Task AssertBackToBackAsync(bool repairsFirst)
    {
        // Leftovers: a failed repair and a failed update; plus one valid file in each folder.
        _ws.StageRepairRaw("storage", "TC-102", Malformed);
        _ws.StageRepair("storage", "TC-101", Edited("TC-101", "Repaired expectation"));
        _ws.StageRaw("storage", "TC-102", Malformed);
        _ws.StageEdit("storage", "TC-103", Edited("TC-103"));

        async Task Repairs()
        {
            var batch = Batch((await _ws.IngestAllAsync("storage", "--keep-baseline")).Out);
            Assert.Equal(["TC-101", "TC-102"], ResultTests(batch));
            Assert.Equal("written", Result(batch, "TC-101").GetProperty("outcome").GetString());
            Assert.All(batch.GetProperty("results").EnumerateArray(),
                r => Assert.StartsWith(".spectra/repairs/", r.GetProperty("file").GetString()));
            Assert.Equal("keep", batch.GetProperty("baseline").GetProperty("mode").GetString());
        }

        async Task Updates()
        {
            var batch = Batch((await _ws.IngestAllAsync()).Out);
            Assert.Equal(["TC-102", "TC-103"], ResultTests(batch));
            Assert.Equal("written", Result(batch, "TC-103").GetProperty("outcome").GetString());
            Assert.All(batch.GetProperty("results").EnumerateArray(),
                r => Assert.StartsWith(".spectra/updates/", r.GetProperty("file").GetString()));
        }

        if (repairsFirst)
        {
            await Repairs();
            await Updates();
        }
        else
        {
            await Updates();
            await Repairs();
        }

        // Spec 090 (FR-B1): a failed file is kept as failed-<id>.json
        Assert.Equal(["failed-TC-102.json"], _ws.RepairFiles("storage"));
        Assert.Equal(["failed-TC-102.json"], _ws.StagedFiles("storage"));
        Assert.Equal("LOAD", await BucketAsync("TC-101")); // repaired with the baseline kept
        Assert.Null(await BucketAsync("TC-103"));          // edited with the baseline rebuilt
    }
}
