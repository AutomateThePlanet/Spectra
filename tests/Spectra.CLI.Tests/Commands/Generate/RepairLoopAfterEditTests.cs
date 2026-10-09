using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 090 T4 (FR-A1, FR-E2): the generation flow's critic → repair → re-critic loop, and the flagged-review repair,
/// still work end to end on the CLI now that an edit drops the old verdict and verdicts carry a fingerprint record.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class RepairLoopAfterEditTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public RepairLoopAfterEditTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        _ws.WriteTest("storage", "TC-102", [Quotas]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private async Task CompileAsync(string id)
    {
        var (exit, _, error) = await _ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
    }

    private static JsonElement Ok((int Exit, string Out, string Err) run)
    {
        Assert.True(run.Exit == 0, run.Err);
        return BaselineTestWorkspace.Json(run.Out);
    }

    [Theory]
    [InlineData("grounded")]
    [InlineData("partial")]
    public async Task TheGenerationRepairLoop_StillWritesTheRepairedVerdict(string reverdict)
    {
        // 8a: critic both; TC-101 grounded, TC-102 partial.
        await CompileAsync("TC-101");
        await CompileAsync("TC-102");
        _ws.WriteVerdict("TC-101", "grounded");
        _ws.WriteVerdict("TC-102", "partial");
        var verdicts = Ok(await _ws.IngestVerdictAllAsync());
        Assert.Equal(1, verdicts.GetProperty("grounded").GetInt32());
        Assert.Equal(1, verdicts.GetProperty("partial").GetInt32());
        var first = Ok(await _ws.IngestGroundingAsync("--suite", "storage", "--all"));
        Assert.Equal(1, first.GetProperty("written").GetInt32());
        Assert.Equal(1, first.GetProperty("skipped_partial_pre_repair").GetInt32());

        // 8b: repair TC-102 (keep-baseline): the old verdict and its record go.
        _ws.StageRepair("storage", "TC-102", _ws.Load("storage", "TC-102") with { ExpectedResult = "Repaired expectation" });
        var repaired = Ok(await _ws.IngestAllAsync("storage", "--keep-baseline"));
        Assert.Equal("written", repaired.GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.False(File.Exists(_ws.VerdictPath("TC-102")));
        Assert.False(File.Exists(_ws.VerdictRecordPath("TC-102")));

        // Re-critic → the repaired block is written.
        await CompileAsync("TC-102");
        _ws.WriteVerdict("TC-102", reverdict);
        Ok(await _ws.IngestVerdictAllAsync());
        var second = Ok(await _ws.IngestGroundingAsync("--suite", "storage", "--all", "--repaired", "--repair-attempts", "1"));

        Assert.Equal(1, second.GetProperty("written").GetInt32());
        Assert.Equal(1, second.GetProperty("skipped_already_written").GetInt32()); // TC-101
        var grounding = _ws.GroundingOf("storage", "TC-102")!;
        Assert.Equal(reverdict == "grounded" ? VerificationVerdict.Grounded : VerificationVerdict.Partial, grounding.Verdict);
        Assert.True(grounding.Repaired);
        Assert.Equal(1, grounding.RepairAttempts);
        Assert.Equal(VerificationVerdict.Grounded, _ws.GroundingOf("storage", "TC-101")!.Verdict);
    }

    [Fact]
    public async Task TheFlaggedReviewRepair_StillWritesTheRepairedVerdict()
    {
        // A flagged partial block, its verdict and record (as after the generation flow).
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "partial");
        await _ws.WriteCriticGroundingAsync("storage", "TC-101", VerificationVerdict.Partial, flagged: true);
        var repairedFile = _ws.PathOf(".spectra/repaired.json");
        File.WriteAllText(repairedFile, "[\n" + UpdatePromptCompiler.SerializeTest(
            _ws.Load("storage", "TC-101") with { ExpectedResult = "Repaired expectation" }) + "\n]\n");

        var edit = Ok(await _ws.RunAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from", repairedFile,
            "--keep-baseline", "--output-format", "json"));

        Assert.Equal("written", edit.GetProperty("outcome").GetString());
        Assert.Null(_ws.GroundingOf("storage", "TC-101"));
        Assert.False(File.Exists(_ws.VerdictPath("TC-101")));

        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        Ok(await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101", "--repaired", "--repair-attempts", "2"));

        var grounding = _ws.GroundingOf("storage", "TC-101")!;
        Assert.Equal(VerificationVerdict.Grounded, grounding.Verdict);
        Assert.True(grounding.Repaired);
        Assert.Equal(2, grounding.RepairAttempts);
    }
}
