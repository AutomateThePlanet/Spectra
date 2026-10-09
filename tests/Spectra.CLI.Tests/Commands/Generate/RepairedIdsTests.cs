using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 091 T4–T6 (F2, FR-006, FR-007): <c>ingest-grounding --all --repaired</c> writes repaired blocks only for the
/// tests named in <c>--ids</c> (the ones re-critiqued after a repair) and refuses without them (<c>IDS_REQUIRED</c>).
/// On 5c10c44 there was no <c>--ids</c>, and a test whose repair failed was stamped <c>repaired: true</c> from its old
/// partial verdict. Workspace from the real <c>spectra init</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class RepairedIdsTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public RepairedIdsTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-130", [Quotas]);
        _ws.WriteTest("storage", "TC-131", [Quotas]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private async Task CompileAsync(string id)
    {
        var (exit, _, error) = await _ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
    }

    private static List<string> Ids(JsonElement result, string property) =>
        result.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToList();

    /// <summary>
    /// Both tests have fresh partial verdicts; TC-130's repair fails to ingest, TC-131's lands and is re-critiqued
    /// (grounded). TC-130's old partial verdict still matches its unchanged content.
    /// </summary>
    private async Task RepairRoundAsync()
    {
        foreach (var id in new[] { "TC-130", "TC-131" })
        {
            await CompileAsync(id);
            _ws.WriteVerdict(id, "partial");
        }

        _ws.StageRepairRaw("storage", "TC-130", "{not json");
        _ws.StageRepair("storage", "TC-131", _ws.Load("storage", "TC-131") with { ExpectedResult = "Repaired expectation" });
        var (_, ingestOutput, ingestError) = await _ws.IngestAllAsync("storage", "--keep-baseline");
        var outcomes = BaselineTestWorkspace.Json(ingestOutput).GetProperty("results").EnumerateArray()
            .ToDictionary(r => r.GetProperty("test").GetString()!, r => r.GetProperty("outcome").GetString());
        Assert.True(outcomes.GetValueOrDefault("TC-130") == "failed", ingestError);
        Assert.True(outcomes.GetValueOrDefault("TC-131") == "written", ingestError);

        await CompileAsync("TC-131");
        _ws.WriteVerdict("TC-131", "grounded");
    }

    // ---- T4 ----

    [Fact]
    public async Task OnlyTheListedReCritiquedTest_GetsARepairedBlock()
    {
        await RepairRoundAsync();

        var (exit, _, error) = await _ws.IngestGroundingAsync(
            "--suite", "storage", "--all", "--repaired", "--repair-attempts", "1", "--ids", "TC-131");

        Assert.True(exit == 0, error);
        var repaired = _ws.GroundingOf("storage", "TC-131");
        Assert.NotNull(repaired);
        Assert.True(repaired!.Repaired);
        Assert.Null(_ws.GroundingOf("storage", "TC-130"));
    }

    // ---- T5 ----

    [Fact]
    public async Task RepairedWithoutIds_IsRefused_AndWritesNothing()
    {
        await RepairRoundAsync();
        var before130 = File.ReadAllBytes(_ws.TestPath("storage", "TC-130"));
        var before131 = File.ReadAllBytes(_ws.TestPath("storage", "TC-131"));

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--repaired", "--repair-attempts", "1");

        Assert.Equal(4, exit);
        Assert.Equal("IDS_REQUIRED", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
        Assert.Equal(before130, File.ReadAllBytes(_ws.TestPath("storage", "TC-130")));
        Assert.Equal(before131, File.ReadAllBytes(_ws.TestPath("storage", "TC-131")));
    }

    // ---- T013 (US2): option rules, id parsing, not_found, the human summary, a pre-091 workspace ----

    private const string BothMessage = "Use either --test <id> or --all, not both.";
    private const string IdsMessage = "--ids is only valid with --all.";

    private async Task AssertUsageErrorAsync(string message, params string[] args)
    {
        var before130 = File.ReadAllBytes(_ws.TestPath("storage", "TC-130"));
        var before131 = File.ReadAllBytes(_ws.TestPath("storage", "TC-131"));

        var (exit, output, error) = await _ws.IngestGroundingAsync(args);

        Assert.Equal(1, exit);
        Assert.Equal(message, error.TrimEnd('\r', '\n'));
        Assert.EndsWith("\n", error);
        Assert.Equal("", output);
        Assert.Equal(before130, File.ReadAllBytes(_ws.TestPath("storage", "TC-130")));
        Assert.Equal(before131, File.ReadAllBytes(_ws.TestPath("storage", "TC-131")));
    }

    private async Task FreshVerdictAsync(string id, string verdict = "grounded")
    {
        await CompileAsync(id);
        _ws.WriteVerdict(id, verdict);
    }

    [Fact]
    public async Task IdsWithoutAll_IsAUsageError()
    {
        await FreshVerdictAsync("TC-131");

        await AssertUsageErrorAsync(IdsMessage, "--suite", "storage", "--test", "TC-131", "--ids", "TC-131");
    }

    [Fact]
    public async Task IdsAlone_IsAUsageError_NotTheEitherMessage()
    {
        await AssertUsageErrorAsync(IdsMessage, "--suite", "storage", "--ids", "TC-131");
    }

    [Fact]
    public async Task TestWithAll_IsAUsageError()
    {
        // FR-006a: a deliberate change — on 5c10c44 --all won and the batch ran.
        await FreshVerdictAsync("TC-131");

        await AssertUsageErrorAsync(BothMessage, "--suite", "storage", "--test", "TC-131", "--all");
    }

    [Fact]
    public async Task TestWithAllRepaired_IsAUsageError_NotIdsRequired()
    {
        await FreshVerdictAsync("TC-131");

        await AssertUsageErrorAsync(BothMessage, "--suite", "storage", "--test", "TC-131", "--all", "--repaired", "--repair-attempts", "1");
    }

    [Fact]
    public async Task RepairedWithIdsEmptyAfterTrimming_IsRefused()
    {
        await FreshVerdictAsync("TC-131", "partial");

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--repaired", "--repair-attempts", "1", "--ids", " , ");

        Assert.Equal(4, exit);
        Assert.Equal("IDS_REQUIRED", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
        Assert.Null(_ws.GroundingOf("storage", "TC-131"));
    }

    [Fact]
    public async Task RepairedWithoutIds_IsRefusedBeforeTheIndexIsRead()
    {
        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "no-such-suite", "--all", "--repaired", "--repair-attempts", "1");

        Assert.Equal(4, exit);
        Assert.Equal("IDS_REQUIRED", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task Ids_AreTrimmed_CaseInsensitive_AndDeDuplicated()
    {
        await FreshVerdictAsync("TC-131");

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--ids", "tc-131, TC-131");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, result.GetProperty("written").GetInt32());
        Assert.Empty(Ids(result, "not_found"));
    }

    [Fact]
    public async Task AnUnknownId_IsReported_EvenWithoutAVerdictsFolder()
    {
        Assert.False(Directory.Exists(_ws.PathOf(".spectra/verdicts")));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--ids", "TC-999");

        Assert.True(exit == 0, error);
        Assert.Equal(new[] { "TC-999" }, Ids(BaselineTestWorkspace.Json(output), "not_found"));
    }

    [Fact]
    public async Task TheHumanSummary_NamesAnUnknownId_OnStdoutInHumanMode_AndStderrInJsonMode()
    {
        await FreshVerdictAsync("TC-131");

        var (humanExit, humanOutput, humanError) = await _ws.RunAsync(
            "ai", "ingest-grounding", "--suite", "storage", "--all", "--ids", "TC-131,TC-999");
        Assert.True(humanExit == 0, humanError);
        Assert.Contains("TC-999: not found in suite 'storage'", humanOutput);

        var (jsonExit, jsonOutput, jsonError) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--ids", "TC-999");
        Assert.True(jsonExit == 0, jsonError);
        BaselineTestWorkspace.Json(jsonOutput); // stdout stays one JSON document
        Assert.Contains("TC-999: not found in suite 'storage'", jsonError);
        Assert.Contains("written=", jsonError);
    }

    [Fact]
    public async Task AllWithoutRepaired_IsLimitedByIds_AndUnchangedWithout()
    {
        await FreshVerdictAsync("TC-130");
        await FreshVerdictAsync("TC-131");

        var (limitedExit, limitedOutput, limitedError) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--ids", "TC-131");
        Assert.True(limitedExit == 0, limitedError);
        Assert.Equal(1, BaselineTestWorkspace.Json(limitedOutput).GetProperty("written").GetInt32());
        Assert.Null(_ws.GroundingOf("storage", "TC-130"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");
        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, result.GetProperty("written").GetInt32()); // TC-130; TC-131 already has its block
        Assert.Empty(Ids(result, "not_found"));
        Assert.NotNull(_ws.GroundingOf("storage", "TC-130"));
    }

    [Fact]
    public async Task ListedManualAndAlreadyGroundedTests_AreReportedAsBefore()
    {
        await FreshVerdictAsync("TC-130");
        await FreshVerdictAsync("TC-131");
        await _ws.WriteManualGroundingAsync("storage", "TC-130");
        await _ws.WriteCriticGroundingAsync("storage", "TC-131");
        var before130 = File.ReadAllBytes(_ws.TestPath("storage", "TC-130"));
        var before131 = File.ReadAllBytes(_ws.TestPath("storage", "TC-131"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--ids", "TC-130,TC-131");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(new[] { "TC-130" }, Ids(result, "manual"));
        Assert.Equal(1, result.GetProperty("skipped_already_written").GetInt32());
        Assert.Equal(0, result.GetProperty("written").GetInt32());
        Assert.Equal(before130, File.ReadAllBytes(_ws.TestPath("storage", "TC-130")));
        Assert.Equal(before131, File.ReadAllBytes(_ws.TestPath("storage", "TC-131")));
    }

    [Fact]
    public async Task APre091Workspace_GivesTheSameRepairedOutcome()
    {
        // FR-015: the T4 scenario with 090 records (no compiled_at).
        await RepairRoundAsync();
        foreach (var id in new[] { "TC-130", "TC-131" })
        {
            var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_ws.VerdictRecordPath(id)))!.AsObject();
            record.Remove("compiled_at");
            File.WriteAllText(_ws.VerdictRecordPath(id), record.ToJsonString());
        }

        var (refusedExit, _, refusedError) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--repaired", "--repair-attempts", "1");
        Assert.Equal(4, refusedExit);
        Assert.Equal("IDS_REQUIRED", BaselineTestWorkspace.Json(refusedError).GetProperty("error_code").GetString());

        var (exit, _, error) = await _ws.IngestGroundingAsync(
            "--suite", "storage", "--all", "--repaired", "--repair-attempts", "1", "--ids", "TC-131");
        Assert.True(exit == 0, error);
        Assert.True(_ws.GroundingOf("storage", "TC-131")!.Repaired);
        Assert.Null(_ws.GroundingOf("storage", "TC-130"));
    }

    // ---- T6 ----

    [Fact]
    public async Task AnUnknownId_IsReportedAsNotFound()
    {
        await CompileAsync("TC-131");
        _ws.WriteVerdict("TC-131", "grounded");

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--ids", "TC-131,TC-999");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(new[] { "TC-999" }, Ids(result, "not_found"));
        Assert.Equal(1, result.GetProperty("written").GetInt32());
        Assert.NotNull(_ws.GroundingOf("storage", "TC-131"));
    }
}
