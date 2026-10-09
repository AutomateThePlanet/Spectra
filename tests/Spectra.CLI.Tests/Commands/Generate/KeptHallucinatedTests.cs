using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 091 T7 (F3, FR-010, FR-011): a test the update flow keeps with a hallucinated verdict is reported by id
/// (<c>skipped_hallucinated</c> / <c>hallucinated</c>), not counted as an error, in <c>ingest-grounding --all</c>. A verdict
/// file that can't be parsed still counts as an error. On 5c10c44 the kept test counted as <c>errors</c>, without its id.
/// Workspace from the real <c>spectra init</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class KeptHallucinatedTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public KeptHallucinatedTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-140", [Quotas]);
        _ws.WriteTest("storage", "TC-141", [Quotas]);
    }

    public void Dispose() => _ws.Dispose();

    private async Task CompileAsync(string id)
    {
        var (exit, _, error) = await _ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
    }

    private static List<string> Ids(JsonElement result, string property) =>
        result.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToList();

    private void WriteUnparseableVerdict(string id)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_ws.VerdictPath(id))!);
        File.WriteAllText(_ws.VerdictPath(id), "{not json");
    }

    // ---- T7 ----

    [Fact]
    public async Task AKeptHallucinatedTest_IsSkippedByName_NotCountedAsAnError()
    {
        await CompileAsync("TC-140");
        _ws.WriteVerdict("TC-140", "hallucinated");
        var before = File.ReadAllBytes(_ws.TestPath("storage", "TC-140"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped_hallucinated").GetInt32());
        Assert.Equal(new[] { "TC-140" }, Ids(result, "hallucinated"));
        Assert.Equal(before, File.ReadAllBytes(_ws.TestPath("storage", "TC-140")));
    }

    // ---- T018 (US3): the order of checks, the drop case, the human summary, --test, one mixed batch, pre-091 ----

    private async Task HallucinatedAsync(string id)
    {
        await CompileAsync(id);
        _ws.WriteVerdict(id, "hallucinated");
    }

    [Fact]
    public async Task AHallucinatedVerdictThatIsAlsoStale_IsListedAsStale()
    {
        await HallucinatedAsync("TC-140");
        File.WriteAllText(_ws.TestPath("storage", "TC-140"), File.ReadAllText(_ws.TestPath("storage", "TC-140")).Replace("It works", "It still works"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(new[] { "TC-140" }, Ids(result, "stale"));
        Assert.Empty(Ids(result, "hallucinated"));
    }

    [Fact]
    public async Task AHallucinatedVerdictForATestWithABlock_IsAlreadyWritten()
    {
        await HallucinatedAsync("TC-140");
        await _ws.WriteCriticGroundingAsync("storage", "TC-140");

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, result.GetProperty("skipped_already_written").GetInt32());
        Assert.Empty(Ids(result, "hallucinated"));
    }

    [Fact]
    public async Task ADroppedTest_IsSkippedAsBefore_NeitherHallucinatedNorAnError()
    {
        // The generation flow's drop: the file is gone while the index and the verdict remain.
        await HallucinatedAsync("TC-141");
        File.Delete(_ws.TestPath("storage", "TC-141"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Empty(Ids(result, "hallucinated"));
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task TheHumanSummary_NamesTheKeptTest_OnStderrInJsonMode_AndStdoutInHumanMode()
    {
        await HallucinatedAsync("TC-140");

        var (jsonExit, jsonOutput, jsonError) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");
        Assert.True(jsonExit == 0, jsonError);
        BaselineTestWorkspace.Json(jsonOutput); // stdout stays one JSON document
        Assert.Contains("TC-140: hallucinated, waiting for review", jsonError);
        Assert.Contains("Batch grounding-ingest for suite 'storage'", jsonError);

        var (humanExit, humanOutput, humanError) = await _ws.RunAsync("ai", "ingest-grounding", "--suite", "storage", "--all");
        Assert.True(humanExit == 0, humanError);
        Assert.Contains("TC-140: hallucinated, waiting for review", humanOutput);
        Assert.Contains("Batch grounding-ingest for suite 'storage'", humanOutput);
    }

    [Fact]
    public async Task TheSingleForm_StillRefusesAHallucinatedVerdict()
    {
        await HallucinatedAsync("TC-140");

        var (exit, _, _) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-140");

        Assert.Equal(4, exit);
        Assert.Null(_ws.GroundingOf("storage", "TC-140"));
    }

    [Fact]
    public async Task OneBatch_KeepsTheHallucinatedTestApart_FromARealError()
    {
        await HallucinatedAsync("TC-140");
        WriteUnparseableVerdict("TC-141");
        var before140 = File.ReadAllBytes(_ws.TestPath("storage", "TC-140"));
        var before141 = File.ReadAllBytes(_ws.TestPath("storage", "TC-141"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, result.GetProperty("errors").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped_hallucinated").GetInt32());
        Assert.Equal(new[] { "TC-140" }, Ids(result, "hallucinated"));
        Assert.Equal(before140, File.ReadAllBytes(_ws.TestPath("storage", "TC-140")));
        Assert.Equal(before141, File.ReadAllBytes(_ws.TestPath("storage", "TC-141")));
    }

    [Fact]
    public async Task APre091Workspace_GivesTheSameOutcome()
    {
        // FR-015: the T7 scenario with a 090 record (no compiled_at).
        await HallucinatedAsync("TC-140");
        var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_ws.VerdictRecordPath("TC-140")))!.AsObject();
        record.Remove("compiled_at");
        File.WriteAllText(_ws.VerdictRecordPath("TC-140"), record.ToJsonString());

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped_hallucinated").GetInt32());
    }

    // ---- T7b (guard, its own row: passes on 5c10c44 and after) ----

    [Fact]
    public async Task AnUnparseableVerdictFile_StillCountsAsAnError()
    {
        WriteUnparseableVerdict("TC-141");
        var before = File.ReadAllBytes(_ws.TestPath("storage", "TC-141"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        Assert.Equal(1, BaselineTestWorkspace.Json(output).GetProperty("errors").GetInt32());
        Assert.Equal(before, File.ReadAllBytes(_ws.TestPath("storage", "TC-141")));
    }
}
