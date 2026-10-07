using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.UpdatePromptWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 088 T5 (FR-014–FR-017): <c>ingest-update --all</c> reports per staging file, deletes what it consumed, keeps
/// what failed; an edit identical to the original is not written but recorded as reviewed (baseline rebuilt unless
/// <c>--keep-baseline</c>).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestUpdateStagingTests : IDisposable
{
    private const string Malformed = "[\n  { \"id\": \"TC-103\", \"title\": }\n]\n";
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    /// <summary>TC-101..TC-103 on Quotas, baselined; then 30GB → 45GB, so every baseline entry differs.</summary>
    public IngestUpdateStagingTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            _ws.WriteTest("storage", id, [Quotas]);
        }

        _ws.InitBaselineAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
    }

    public void Dispose() => _ws.Dispose();

    private Task<JsonElement> IngestAllAsync(params string[] extra) =>
        _ws.RunJsonAsync(["ai", "ingest-update", "storage", "--all", .. extra]);

    private void StageEdited(string id) =>
        _ws.StageEdit("storage", id, _ws.Load("storage", id) with { ExpectedResult = "Each user gets 45GB" });

    private void StageIdentical(string id) => _ws.StageEdit("storage", id, _ws.Load("storage", id));

    private static JsonElement Result(JsonElement batch, string id) =>
        batch.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("test").GetString() == id);

    private async Task<bool> InTriageAsync(string id) =>
        BaselineTestWorkspace.GroupOf(await _ws.TriageAsync("storage", "--dry-run"), id).ValueKind != JsonValueKind.Undefined;

    [Fact]
    public async Task EditedUnchangedMalformed_OneWrittenOneUnchangedOneFailed_ConsumedFilesDeleted()
    {
        StageEdited("TC-101");
        StageIdentical("TC-102");
        _ws.StageRaw("storage", "TC-103", Malformed);
        var tc102Before = File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md"));
        var tc102FpBefore = _ws.RefFingerprint("storage", "TC-102", Quotas);

        var batch = await IngestAllAsync();

        Assert.Equal(1, batch.GetProperty("written").GetInt32());
        Assert.Equal(1, batch.GetProperty("unchanged").GetInt32());
        Assert.Equal(1, batch.GetProperty("errors").GetInt32());
        Assert.Equal(0, batch.GetProperty("skipped_no_original").GetInt32());
        Assert.Equal("written", Result(batch, "TC-101").GetProperty("outcome").GetString());
        Assert.Equal("unchanged", Result(batch, "TC-102").GetProperty("outcome").GetString());
        var failed = Result(batch, "TC-103");
        Assert.Equal("failed", failed.GetProperty("outcome").GetString());
        Assert.Equal("MALFORMED_JSON", failed.GetProperty("error_code").GetString());
        Assert.Equal(".spectra/updates/storage/updated-TC-103.json", failed.GetProperty("file").GetString());

        Assert.Contains("Each user gets 45GB", File.ReadAllText(_ws.PathOf("test-cases/storage/TC-101.md")));
        Assert.Equal(tc102Before, File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md")));
        Assert.NotEqual(tc102FpBefore, _ws.RefFingerprint("storage", "TC-102", Quotas)); // rebuilt: reviewed
        Assert.False(await InTriageAsync("TC-101"));
        Assert.False(await InTriageAsync("TC-102"));
        Assert.True(await InTriageAsync("TC-103"));
        Assert.Equal(["updated-TC-103.json"], _ws.StagedFiles("storage"));

        var again = await IngestAllAsync();

        Assert.Equal(0, again.GetProperty("written").GetInt32());
        Assert.Equal(0, again.GetProperty("unchanged").GetInt32());
        Assert.Equal(1, again.GetProperty("errors").GetInt32());
        Assert.Equal("TC-103", Assert.Single(again.GetProperty("results").EnumerateArray()).GetProperty("test").GetString());
    }

    [Fact]
    public async Task NoOriginal_IsFailedAndKept()
    {
        _ws.StageEdit("storage", "TC-999", _ws.Load("storage", "TC-101") with { Id = "TC-999" });

        var batch = await IngestAllAsync();

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

        var batch = await IngestAllAsync();

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
        StageIdentical("TC-102");
        var baselineBefore = _ws.ReadBaseline("storage");

        var batch = await IngestAllAsync("--keep-baseline");

        Assert.Equal("unchanged", Result(batch, "TC-102").GetProperty("outcome").GetString());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.True(await InTriageAsync("TC-102"));
        Assert.Empty(_ws.StagedFiles("storage"));
    }

    [Fact]
    public async Task SingleForm_IdenticalEdit_IsUnchanged_BaselineRebuilt_UnlessKeepBaseline()
    {
        var staged = _ws.StageEdit("storage", "TC-102", _ws.Load("storage", "TC-102"));
        var fileBefore = File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md"));
        var baselineBefore = _ws.ReadBaseline("storage");

        var kept = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-102", "--from", staged, "--keep-baseline");

        Assert.True(kept.GetProperty("unchanged").GetBoolean());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-102", "--from", staged);

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("unchanged").GetBoolean());
        Assert.Equal(0, result.GetProperty("persisted").GetInt32());
        Assert.Equal(fileBefore, File.ReadAllBytes(_ws.PathOf("test-cases/storage/TC-102.md")));
        Assert.False(await InTriageAsync("TC-102"));

        var (exit, human, _) = await _ws.RunAsync("ai", "ingest-update", "storage", "--test-id", "TC-102", "--from", staged);
        Assert.Equal(0, exit);
        Assert.Contains("Test 'TC-102' needs no change; its baseline was refreshed.", human);
    }

    [Fact]
    public async Task TheHumanSummary_ListsEachTest_InBothModes()
    {
        StageEdited("TC-101");
        StageIdentical("TC-102");
        _ws.StageRaw("storage", "TC-103", Malformed);
        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-update", "storage", "--all", "--output-format", "json");

        Assert.Equal(0, exit);
        Assert.Equal(JsonValueKind.Object, BaselineTestWorkspace.Json(output).ValueKind); // stdout stays one JSON document
        Assert.Contains("TC-101: updated", error);
        Assert.Contains("TC-102: unchanged (reviewed, no edit needed)", error);
        Assert.Contains("TC-103: failed [MALFORMED_JSON]", error);

        var (humanExit, human, _) = await _ws.RunAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal(0, humanExit);
        Assert.Contains("written=0 unchanged=0 skipped_no_original=0 errors=1", human);
        Assert.Contains("TC-103: failed [MALFORMED_JSON]", human);
    }
}
