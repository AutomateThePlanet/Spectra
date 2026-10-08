using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.UpdatePromptWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 088 T5 (FR-014–FR-017): <c>ingest-update --all</c> reports per staging file, deletes what it consumed, keeps
/// what failed; an edit identical to the original is not written but recorded as reviewed (baseline rebuilt unless
/// <c>--keep-baseline</c>). Spec 089 changed four expectations here deliberately: a batch with a failed file exits 8
/// (F5); an identical edit of a test whose LOAD change is still pending is <c>unchanged_pending</c> and records nothing
/// (F3) — the "rebuilt" case now uses a LIGHT change; and repairs (<c>--keep-baseline</c>) are staged in
/// <c>.spectra/repairs/</c> (F1).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestUpdateStagingTests : IDisposable
{
    private const string Malformed = "[\n  { \"id\": \"TC-103\", \"title\": }\n]\n";
    private const string Quotas = "docs/storage.md#Quotas";
    private const string Sharing = "docs/storage.md#Sharing";
    private const string Pending = "returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again)";

    private static string Doc(string quota, string share) =>
        $"# Storage\n\n## Quotas\n\nEach user gets **{quota}** of storage space.\n\n## Sharing\n\nFiles can be shared {share} with any registered user.\n";

    private readonly BaselineTestWorkspace _ws = new();

    /// <summary>
    /// TC-101..TC-103 on Quotas and TC-104 on Sharing, baselined; then 30GB → 45GB (LOAD for TC-101..TC-103) and
    /// "by link" → "through a link" (LIGHT for TC-104).
    /// </summary>
    public IngestUpdateStagingTests()
    {
        _ws.WriteDoc("docs/storage.md", Doc("30GB", "by link"));
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            _ws.WriteTest("storage", id, [Quotas]);
        }

        _ws.WriteTest("storage", "TC-104", [Sharing]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", Doc("45GB", "through a link"));
    }

    public void Dispose() => _ws.Dispose();

    private async Task<(int Exit, JsonElement Batch)> IngestAllAsync(params string[] extra)
    {
        var (exit, output, _) = await _ws.IngestAllAsync("storage", extra);
        return (exit, BaselineTestWorkspace.Json(output));
    }

    private void StageEdited(string id) =>
        _ws.StageEdit("storage", id, _ws.Load("storage", id) with { ExpectedResult = "Each user gets 45GB" });

    private void StageIdentical(string id) => _ws.StageEdit("storage", id, _ws.Load("storage", id));

    private static JsonElement Result(JsonElement batch, string id) =>
        batch.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("test").GetString() == id);

    private async Task<string?> BucketAsync(string id)
    {
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync("storage", "--dry-run"), id);
        return group.ValueKind == JsonValueKind.Undefined ? null : group.GetProperty("bucket").GetString();
    }

    private async Task<bool> InTriageAsync(string id) => await BucketAsync(id) is not null;

    [Fact]
    public async Task EditedPendingMalformed_OneWrittenOnePendingOneFailed_ConsumedFilesDeleted()
    {
        StageEdited("TC-101");
        StageIdentical("TC-102"); // its LOAD change is still pending (Spec 089 F3)
        _ws.StageRaw("storage", "TC-103", Malformed);
        var tc102Before = File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md"));
        var tc102FpBefore = _ws.RefFingerprint("storage", "TC-102", Quotas);

        var (exit, batch) = await IngestAllAsync();

        Assert.Equal(8, exit); // Spec 089 (F5): TC-103 failed
        Assert.Equal(1, batch.GetProperty("written").GetInt32());
        Assert.Equal(0, batch.GetProperty("unchanged").GetInt32());
        Assert.Equal(1, batch.GetProperty("unchanged_pending").GetInt32());
        Assert.Equal(1, batch.GetProperty("errors").GetInt32());
        Assert.Equal(0, batch.GetProperty("skipped_no_original").GetInt32());
        Assert.Equal("written", Result(batch, "TC-101").GetProperty("outcome").GetString());
        var pending = Result(batch, "TC-102");
        Assert.Equal("unchanged_pending", pending.GetProperty("outcome").GetString());
        Assert.Equal("LOAD", pending.GetProperty("bucket").GetString());
        var failed = Result(batch, "TC-103");
        Assert.Equal("failed", failed.GetProperty("outcome").GetString());
        Assert.Equal("MALFORMED_JSON", failed.GetProperty("error_code").GetString());
        Assert.Equal(".spectra/updates/storage/updated-TC-103.json", failed.GetProperty("file").GetString());

        Assert.Contains("Each user gets 45GB", File.ReadAllText(_ws.PathOf("test-cases/storage/TC-101.md")));
        Assert.Equal(tc102Before, File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md")));
        Assert.Equal(tc102FpBefore, _ws.RefFingerprint("storage", "TC-102", Quotas)); // not recorded: still pending
        Assert.False(await InTriageAsync("TC-101"));
        Assert.Equal("LOAD", await BucketAsync("TC-102"));
        Assert.True(await InTriageAsync("TC-103"));
        Assert.Equal(["updated-TC-103.json"], _ws.StagedFiles("storage"));

        var (againExit, again) = await IngestAllAsync();

        Assert.Equal(8, againExit);
        Assert.Equal(0, again.GetProperty("written").GetInt32());
        Assert.Equal(0, again.GetProperty("unchanged").GetInt32());
        Assert.Equal(0, again.GetProperty("unchanged_pending").GetInt32());
        Assert.Equal(1, again.GetProperty("errors").GetInt32());
        Assert.Equal("TC-103", Assert.Single(again.GetProperty("results").EnumerateArray()).GetProperty("test").GetString());
    }

    [Fact]
    public async Task NoOriginal_IsFailedAndKept()
    {
        _ws.StageEdit("storage", "TC-999", _ws.Load("storage", "TC-101") with { Id = "TC-999" });

        var (exit, batch) = await IngestAllAsync();

        Assert.Equal(8, exit);
        Assert.Equal(1, batch.GetProperty("skipped_no_original").GetInt32());
        Assert.Equal(0, batch.GetProperty("errors").GetInt32());
        var result = Result(batch, "TC-999");
        Assert.Equal("failed", result.GetProperty("outcome").GetString());
        Assert.Equal("NO_ORIGINAL", result.GetProperty("error_code").GetString());
        Assert.Equal(["updated-TC-999.json"], _ws.StagedFiles("storage"));
    }

    [Fact]
    public async Task AStagedIdOtherThanTheFileName_IsIdMismatch_AndKept()
    {
        var tc101Before = File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-101.md"));
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-102") with { ExpectedResult = "Each user gets 45GB" });

        var (exit, batch) = await IngestAllAsync();

        Assert.Equal(8, exit);
        var result = Result(batch, "TC-101");
        Assert.Equal("failed", result.GetProperty("outcome").GetString());
        Assert.Equal("ID_MISMATCH", result.GetProperty("error_code").GetString());
        Assert.Contains("TC-102", result.GetProperty("errors")[0].GetString());
        Assert.Equal(tc101Before, File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-101.md")));
        Assert.Equal(["updated-TC-101.json"], _ws.StagedFiles("storage"));
    }

    [Fact]
    public async Task KeepBaseline_UnchangedEdit_LeavesTheBaselineAsItWas_AndDeletesTheStagingFile()
    {
        _ws.StageRepair("storage", "TC-102", _ws.Load("storage", "TC-102"));
        var baselineBefore = _ws.ReadBaseline("storage");

        var (exit, batch) = await IngestAllAsync("--keep-baseline");

        Assert.Equal(0, exit);
        Assert.Equal("unchanged", Result(batch, "TC-102").GetProperty("outcome").GetString());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.True(await InTriageAsync("TC-102"));
        Assert.Empty(_ws.RepairFiles("storage"));
    }

    [Fact]
    public async Task SingleForm_IdenticalEdit_PendingLoadIsNotRecorded_LightIsRebuilt_UnlessKeepBaseline()
    {
        // LOAD pending (Spec 089 F3): nothing recorded, the test stays LOAD.
        var load = _ws.StageEdit("storage", "TC-102", _ws.Load("storage", "TC-102"));
        var tc102Before = File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md"));
        var baselineBefore = _ws.ReadBaseline("storage");

        var pending = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-102", "--from", load);

        Assert.Equal("unchanged_pending", pending.GetProperty("outcome").GetString());
        Assert.Equal(0, pending.GetProperty("persisted").GetInt32());
        Assert.Equal(tc102Before, File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md")));
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));

        // LIGHT: reviewed — kept with --keep-baseline, rebuilt without.
        var light = _ws.StageEdit("storage", "TC-104", _ws.Load("storage", "TC-104"));
        var tc104Before = File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-104.md"));

        var kept = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-104", "--from", light, "--keep-baseline");

        Assert.Equal("unchanged", kept.GetProperty("outcome").GetString());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.Equal("LIGHT", await BucketAsync("TC-104"));

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-104", "--from", light);

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal("unchanged", result.GetProperty("outcome").GetString());
        Assert.True(result.GetProperty("unchanged").GetBoolean());
        Assert.Equal(0, result.GetProperty("persisted").GetInt32());
        Assert.Equal(tc104Before, File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-104.md")));
        Assert.False(await InTriageAsync("TC-104"));

        var (exit, human, _) = await _ws.RunAsync("ai", "ingest-update", "storage", "--test-id", "TC-104", "--from", light);
        Assert.Equal(0, exit);
        Assert.Contains("Test 'TC-104' needs no change; its baseline was refreshed.", human);
    }

    [Fact]
    public async Task TheHumanSummary_ListsEachTest_InBothModes()
    {
        StageEdited("TC-101");
        StageIdentical("TC-102");
        _ws.StageRaw("storage", "TC-103", Malformed);
        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-update", "storage", "--all", "--output-format", "json");

        Assert.Equal(8, exit);
        Assert.Equal(JsonValueKind.Object, BaselineTestWorkspace.Json(output).ValueKind); // stdout stays one JSON document
        Assert.Contains("TC-101: updated", error);
        Assert.Contains($"TC-102: {Pending}", error);
        Assert.Contains("TC-103: failed [MALFORMED_JSON]", error);

        var (humanExit, human, _) = await _ws.RunAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal(8, humanExit);
        Assert.Contains("written=0 unchanged=0 unchanged_pending=0 skipped_no_original=0 errors=1", human);
        Assert.Contains("TC-103: failed [MALFORMED_JSON]", human);
    }
}
