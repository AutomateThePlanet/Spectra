using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 090 T3 (FR-A3): <c>ingest-grounding</c> never overwrites a Manual verdict — <c>MANUAL_VERDICT</c> (exit 4) in the
/// single form, <c>skipped_manual</c> in <c>--all</c> (checked before the already-written skip).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ManualVerdictTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public ManualVerdictTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
        _ws.WriteManualGroundingAsync("storage", "TC-101").GetAwaiter().GetResult();
        _ws.WriteVerdict("TC-101", "grounded");
    }

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task TheSingleForm_RefusesManualVerdict_AndWritesNothing()
    {
        var before = File.ReadAllBytes(_ws.TestPath("storage", "TC-101"));

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.Equal(4, exit);
        Assert.Equal("MANUAL_VERDICT", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
        Assert.Equal(before, File.ReadAllBytes(_ws.TestPath("storage", "TC-101")));
    }

    [Fact]
    public async Task TheBatch_SkipsManualVerdicts_BeforeTheAlreadyWrittenSkip()
    {
        await _ws.WriteCriticGroundingAsync("storage", "TC-102");
        _ws.WriteVerdict("TC-102", "grounded");
        var before = File.ReadAllBytes(_ws.TestPath("storage", "TC-101"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var batch = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, batch.GetProperty("skipped_manual").GetInt32());
        Assert.Equal(["TC-101"], batch.GetProperty("manual").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, batch.GetProperty("skipped_already_written").GetInt32()); // TC-102 only
        Assert.Equal(before, File.ReadAllBytes(_ws.TestPath("storage", "TC-101")));
    }

    [Fact]
    public async Task AFreshRecord_DoesNotLetTheCriticReplaceAManualVerdict()
    {
        var (compileExit, _, compileError) = await _ws.CompileCriticAsync("storage", "TC-101");
        Assert.True(compileExit == 0, compileError);

        var (exit, _, _) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.Equal(4, exit);
        Assert.Equal(VerificationVerdict.Manual, _ws.GroundingOf("storage", "TC-101")!.Verdict);
    }
}
