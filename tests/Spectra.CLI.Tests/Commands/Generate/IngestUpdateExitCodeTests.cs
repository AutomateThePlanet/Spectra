using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 089 T5 (F5, FR-018–FR-019): <c>ingest-update --all</c> exits 8 when at least one staged file failed — whatever the
/// code, even when every file failed — and 0 when none did (<c>unchanged_pending</c> is not a failure). The JSON, the
/// deletions and the kept files are the same as for exit 0; whole-command errors stay exit 1. <c>UNREADABLE</c> and
/// <c>PERSIST_FAILED</c> are covered by the same "any failed outcome → 8" rule, asserted here over the codes that can be
/// staged. On 6ea7520 every failure row exits 0.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestUpdateExitCodeTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public IngestUpdateExitCodeTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            _ws.WriteTest("storage", id, [Quotas]);
        }

        _ws.InitBaselineAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private TestCase Edited(string id) => _ws.Load("storage", id) with { ExpectedResult = $"Edited {id}" };

    private static string Array(params TestCase[] tests) =>
        "[\n" + string.Join(",\n", tests.Select(UpdatePromptCompiler.SerializeTest)) + "\n]\n";

    private static HashSet<string> Keys(JsonElement json) => json.EnumerateObject().Select(p => p.Name).ToHashSet();

    /// <summary>Stages a file that fails with <paramref name="code"/> for TC-102; returns its file name.</summary>
    private string StageFailing(string code)
    {
        switch (code)
        {
            case "NO_ORIGINAL":
                _ws.StageEdit("storage", "TC-999", Edited("TC-101") with { Id = "TC-999" });
                return "updated-TC-999.json";
            case "ID_MISMATCH":
                _ws.StageEdit("storage", "TC-102", Edited("TC-103"));
                break;
            case "MALFORMED_JSON":
                _ws.StageRaw("storage", "TC-102", "[\n  { \"id\": \"TC-102\", \"title\": }\n]\n");
                break;
            case "SCHEMA_INVALID":
                _ws.StageEdit("storage", "TC-102", _ws.Load("storage", "TC-102") with { ExpectedResult = "" });
                break;
            case "DRIFT_DETECTED":
                _ws.StageEdit("storage", "TC-102", Edited("TC-102") with { Priority = Priority.Low });
                break;
            case "MULTIPLE_TESTS":
                _ws.StageRaw("storage", "TC-102", Array(Edited("TC-102"), Edited("TC-103")));
                break;
        }

        return "updated-TC-102.json";
    }

    [Theory]
    [InlineData("NO_ORIGINAL")]
    [InlineData("ID_MISMATCH")]
    [InlineData("MALFORMED_JSON")]
    [InlineData("SCHEMA_INVALID")]
    [InlineData("DRIFT_DETECTED")]
    [InlineData("MULTIPLE_TESTS")]
    public async Task OneFailedFile_Exits8_WithTheSameJson_ValidFileWrittenAndDeleted_FailedKept(string code)
    {
        _ws.StageEdit("storage", "TC-101", Edited("TC-101"));
        var failing = StageFailing(code);

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 8, $"exit {exit}: {error}");
        var batch = BaselineTestWorkspace.Json(output);
        var failed = batch.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("outcome").GetString() == "failed");
        Assert.Equal(code, failed.GetProperty("error_code").GetString());
        Assert.Equal(1, batch.GetProperty("written").GetInt32());
        Assert.Contains("Edited TC-101", File.ReadAllText(_ws.PathOf("test-cases/storage/TC-101.md")));
        Assert.Equal([failing], _ws.StagedFiles("storage"));

        // Same JSON shape as a clean batch.
        File.Delete(_ws.PathOf($".spectra/updates/storage/{failing}"));
        _ws.StageEdit("storage", "TC-103", Edited("TC-103"));
        var (cleanExit, cleanOutput, _) = await _ws.IngestAllAsync();
        Assert.Equal(0, cleanExit);
        Assert.Equal(Keys(BaselineTestWorkspace.Json(cleanOutput)), Keys(batch));
    }

    [Fact]
    public async Task EveryFileFailed_Exits8()
    {
        StageFailing("MALFORMED_JSON");
        StageFailing("NO_ORIGINAL");

        var (exit, output, _) = await _ws.IngestAllAsync();

        Assert.Equal(8, exit);
        Assert.Equal(0, BaselineTestWorkspace.Json(output).GetProperty("written").GetInt32());
        Assert.Equal(2, _ws.StagedFiles("storage").Length);
    }

    [Fact]
    public async Task OnlyWrittenAndUnchanged_Exits0()
    {
        _ws.StageEdit("storage", "TC-101", Edited("TC-101"));
        _ws.StageEdit("storage", "TC-102", _ws.Load("storage", "TC-102"));

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
        var batch = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, batch.GetProperty("written").GetInt32());
        Assert.Equal(1, batch.GetProperty("unchanged").GetInt32());
    }

    [Fact]
    public async Task AnUnchangedPendingOutcomeAlone_Exits0()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));

        var (exit, _, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEmptyOrMissingFolder_Exits0(bool create)
    {
        if (create)
        {
            Directory.CreateDirectory(_ws.PathOf(".spectra/updates/storage"));
        }

        var (exit, _, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
    }

    [Fact]
    public async Task AFailedRepairFile_Exits8()
    {
        _ws.StageRepairRaw("storage", "TC-102", "[\n  { \"id\": \"TC-102\", \"title\": }\n]\n");

        var (exit, output, _) = await _ws.IngestAllAsync("storage", "--keep-baseline");

        Assert.Equal(8, exit);
        Assert.Equal("MALFORMED_JSON", BaselineTestWorkspace.Json(output).GetProperty("results")[0].GetProperty("error_code").GetString());
        Assert.Equal(["repaired-TC-102.json"], _ws.RepairFiles("storage"));
    }

    [Fact]
    public async Task AnUnreadableBaseline_IsStillExit1_AndNothingIsDeleted()
    {
        _ws.StageEdit("storage", "TC-101", Edited("TC-101"));
        File.WriteAllText(_ws.BaselinePath("storage"), "{ not json");

        var (exit, _, error) = await _ws.IngestAllAsync();

        Assert.Equal(1, exit);
        Assert.Equal("BASELINE_UNREADABLE", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
        Assert.Equal(["updated-TC-101.json"], _ws.StagedFiles("storage"));
    }
}
