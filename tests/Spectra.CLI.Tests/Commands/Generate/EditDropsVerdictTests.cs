using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 090 T1 (FR-A1): a written update edit — single form, <c>--all</c>, or a repair with <c>--keep-baseline</c> —
/// removes the test's critic grounding block in the same write and deletes its verdict file and fingerprint record, so
/// <c>audit-grounding</c> shows the test unverified. A Manual verdict is kept; unchanged and unchanged_pending touch
/// nothing.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class EditDropsVerdictTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public EditDropsVerdictTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        _ws.WriteTest("storage", "TC-102", [Quotas]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>A critic block, the verdict file and the fingerprint record, as after a critic pass.</summary>
    private async Task VerifiedAsync(BaselineTestWorkspace ws, string id)
    {
        var (exit, _, error) = await ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
        ws.WriteVerdict(id, "grounded");
        await ws.WriteCriticGroundingAsync("storage", id);
    }

    /// <summary>Ingests an edit of <paramref name="id"/> in the given form; returns (exit, stdout, stderr).</summary>
    private static async Task<(int Exit, string Out, string Err)> IngestEditAsync(
        BaselineTestWorkspace ws, string form, string id, Spectra.Core.Models.TestCase edited)
    {
        switch (form)
        {
            case "single":
                var from = ws.PathOf($".spectra/edit-{id}.json");
                File.WriteAllText(from, "[\n" + Spectra.CLI.Generation.UpdatePromptCompiler.SerializeTest(edited) + "\n]\n");
                return await ws.RunAsync("ai", "ingest-update", "storage", "--test-id", id, "--from", from, "--output-format", "json");
            case "all":
                ws.StageEdit("storage", id, edited);
                return await ws.IngestAllAsync();
            default:
                ws.StageRepair("storage", id, edited);
                return await ws.IngestAllAsync("storage", "--keep-baseline");
        }
    }

    private static JsonElement AuditEntry(string audit, string id) =>
        BaselineTestWorkspace.Json(audit).GetProperty("tests").EnumerateArray().Single(t => t.GetProperty("id").GetString() == id);

    [Theory]
    [InlineData("single")]
    [InlineData("all")]
    [InlineData("repair")]
    public async Task AWrittenEdit_DropsTheCriticBlock_TheVerdictAndTheRecord(string form)
    {
        await VerifiedAsync(_ws, "TC-101");
        var edited = _ws.Load("storage", "TC-101") with { ExpectedResult = "The quota is 30GB per user", Grounding = null };

        var (exit, _, error) = await IngestEditAsync(_ws, form, "TC-101", edited);

        Assert.True(exit == 0, error);
        var text = File.ReadAllText(_ws.TestPath("storage", "TC-101"));
        Assert.DoesNotContain("grounding:", text);
        Assert.Contains("The quota is 30GB per user", text);
        Assert.Null(_ws.GroundingOf("storage", "TC-101"));
        Assert.False(File.Exists(_ws.VerdictPath("TC-101")));
        Assert.False(File.Exists(_ws.VerdictRecordPath("TC-101")));

        var (_, audit, _) = await _ws.AuditGroundingJsonAsync();
        var entry = AuditEntry(audit, "TC-101");
        Assert.Equal("none", entry.GetProperty("verdict").GetString());
        Assert.False(entry.GetProperty("grounding_written").GetBoolean());
        Assert.Equal("critic", entry.GetProperty("action_needed").GetString());
    }

    [Theory]
    [InlineData("single")]
    [InlineData("all")]
    public async Task AManualVerdict_IsKept_AndReported(string form)
    {
        await _ws.WriteManualGroundingAsync("storage", "TC-101");
        var before = _ws.GroundingOf("storage", "TC-101");
        var edited = _ws.Load("storage", "TC-101") with { ExpectedResult = "The quota is 30GB per user" };

        var (exit, output, error) = await IngestEditAsync(_ws, form, "TC-101", edited);

        Assert.True(exit == 0, error);
        var after = _ws.GroundingOf("storage", "TC-101");
        Assert.Equal(VerificationVerdict.Manual, after!.Verdict);
        Assert.Equal(before!.Note, after.Note);
        Assert.Equal(before.VerifiedAt, after.VerifiedAt);
        var json = BaselineTestWorkspace.Json(output);
        if (form == "single")
        {
            Assert.True(json.GetProperty("manual_verdict_kept").GetBoolean());
        }
        else
        {
            Assert.Equal(["TC-101"], json.GetProperty("manual_verdict_kept").EnumerateArray().Select(e => e.GetString()));
            Assert.Contains("TC-101: updated (manual verdict kept)", error);
        }
    }

    [Fact]
    public async Task AnUnchangedEdit_TouchesNoVerdict()
    {
        await VerifiedAsync(_ws, "TC-101");
        var before = File.ReadAllBytes(_ws.TestPath("storage", "TC-101"));
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
        Assert.Equal("unchanged", BaselineTestWorkspace.Json(output).GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Equal(before, File.ReadAllBytes(_ws.TestPath("storage", "TC-101")));
        Assert.True(File.Exists(_ws.VerdictPath("TC-101")));
        Assert.True(File.Exists(_ws.VerdictRecordPath("TC-101")));
    }

    [Fact]
    public async Task AnUnchangedPendingEdit_TouchesNoVerdict()
    {
        await VerifiedAsync(_ws, "TC-101");
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
        var before = File.ReadAllBytes(_ws.TestPath("storage", "TC-101"));
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
        Assert.Equal("unchanged_pending", BaselineTestWorkspace.Json(output).GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Equal(before, File.ReadAllBytes(_ws.TestPath("storage", "TC-101")));
        Assert.True(File.Exists(_ws.VerdictPath("TC-101")));
    }

    [Fact]
    public async Task AnotherTestsVerdict_IsLeftAlone()
    {
        await VerifiedAsync(_ws, "TC-101");
        await VerifiedAsync(_ws, "TC-102");

        var (exit, _, error) = await IngestEditAsync(_ws, "all", "TC-101",
            _ws.Load("storage", "TC-101") with { ExpectedResult = "Edited", Grounding = null });

        Assert.True(exit == 0, error);
        Assert.True(File.Exists(_ws.VerdictPath("TC-102")));
        Assert.True(File.Exists(_ws.VerdictRecordPath("TC-102")));
        Assert.NotNull(_ws.GroundingOf("storage", "TC-102"));
    }

    [Fact]
    public async Task RealInitWorkspace_AWrittenEditDropsTheVerdict()
    {
        using var ws = new BaselineTestWorkspace(writeConfig: false);
        await ws.InitWithRealTemplateAsync();
        ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        var (ingestExit, _, ingestError) = await ws.RunAsync("ai", "ingest-tests", "storage", "--from",
            ws.GeneratedPayload(("TC-101", [Quotas], [])), "--output-format", "json");
        Assert.True(ingestExit == 0, ingestError);
        await VerifiedAsync(ws, "TC-101");

        var (exit, _, error) = await IngestEditAsync(ws, "all", "TC-101",
            ws.Load("storage", "TC-101") with { ExpectedResult = "Edited", Grounding = null });

        Assert.True(exit == 0, error);
        Assert.Null(ws.GroundingOf("storage", "TC-101"));
        Assert.False(File.Exists(ws.VerdictPath("TC-101")));
    }
}
