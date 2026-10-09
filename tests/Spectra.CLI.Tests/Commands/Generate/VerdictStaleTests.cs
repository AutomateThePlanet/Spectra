using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 090 T2 (FR-A2): <c>compile-critic-prompt</c> records the fingerprint of the content the critic is given;
/// <c>ingest-verdict</c> and <c>ingest-grounding</c> refuse a verdict whose test has changed since (<c>VERDICT_STALE</c>).
/// A verdict without a record (from before 090) is accepted as before.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class VerdictStaleTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public VerdictStaleTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            _ws.WriteTest("storage", id, [Quotas]);
        }
    }

    public void Dispose() => _ws.Dispose();

    private JsonElement Record(string id) => BaselineTestWorkspace.Json(File.ReadAllText(_ws.VerdictRecordPath(id)));

    private async Task CompileAsync(string id)
    {
        var (exit, _, error) = await _ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
    }

    private void EditByHand(string id) =>
        File.WriteAllText(_ws.TestPath("storage", id), File.ReadAllText(_ws.TestPath("storage", id)).Replace("It works", "It still works"));

    [Fact]
    public async Task CompileCriticPrompt_RecordsTheFingerprint_PerTest()
    {
        await CompileAsync("TC-101");

        var record = Record("TC-101");
        Assert.Equal("TC-101", record.GetProperty("id").GetString());
        Assert.Equal("storage", record.GetProperty("suite").GetString());
        Assert.Equal("test-cases/storage/TC-101.md", record.GetProperty("file").GetString());
        Assert.StartsWith("sha256:", record.GetProperty("fingerprint").GetString());
        Assert.False(File.Exists(_ws.VerdictRecordPath("TC-102")));
    }

    [Fact]
    public async Task TheSuiteWideForm_RecordsEveryTest()
    {
        var (exit, _, error) = await _ws.RunAsync("ai", "compile-critic-prompt", "--suite", "storage");

        Assert.True(exit == 0, error);
        Assert.All(new[] { "TC-101", "TC-102", "TC-103" }, id => Assert.True(File.Exists(_ws.VerdictRecordPath(id)), id));
    }

    [Fact]
    public async Task TheLegacyPathForm_RecordsWithoutASuite()
    {
        var (exit, _, error) = await _ws.RunAsync(
            "ai", "compile-critic-prompt", "--test", "test-cases/storage/TC-101.md", "--docs", "docs/storage.md");

        Assert.True(exit == 0, error);
        Assert.Equal(JsonValueKind.Null, Record("TC-101").GetProperty("suite").ValueKind);
    }

    [Fact]
    public async Task AFailedCompile_RecordsNothing()
    {
        File.WriteAllText(_ws.TestPath("storage", "TC-101"), "---\nid: [unterminated\n---\n\n# Broken\n");

        var (exit, _, _) = await _ws.CompileCriticAsync("storage", "TC-101");

        Assert.NotEqual(0, exit);
        Assert.False(File.Exists(_ws.VerdictRecordPath("TC-101")));
    }

    [Fact]
    public async Task WritingTheGroundingBlock_DoesNotChangeTheFingerprint()
    {
        await CompileAsync("TC-101");
        var before = Record("TC-101").GetProperty("fingerprint").GetString();
        _ws.WriteVerdict("TC-101", "grounded");

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.True(exit == 0, error);
        Assert.NotNull(_ws.GroundingOf("storage", "TC-101"));
        await CompileAsync("TC-101");
        Assert.Equal(before, Record("TC-101").GetProperty("fingerprint").GetString());
    }

    [Fact]
    public async Task AStaleVerdict_IsRefusedInTheSingleForms()
    {
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        EditByHand("TC-101");
        var fileBefore = File.ReadAllBytes(_ws.TestPath("storage", "TC-101"));

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.Equal(4, exit);
        var failure = BaselineTestWorkspace.Json(error);
        Assert.Equal("VERDICT_STALE", failure.GetProperty("error_code").GetString());
        Assert.False(failure.GetProperty("success").GetBoolean());
        Assert.Contains("TC-101", failure.GetProperty("errors")[0].GetString());
        Assert.Equal(fileBefore, File.ReadAllBytes(_ws.TestPath("storage", "TC-101")));

        var (verdictExit, _, verdictError) = await _ws.RunAsync(
            "ai", "ingest-verdict", "--from", ".spectra/verdicts/critic-verdict-TC-101.json", "--output-format", "json");

        Assert.Equal(4, verdictExit);
        Assert.Equal("VERDICT_STALE", BaselineTestWorkspace.Json(verdictError).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task AStaleVerdict_IsSkippedInTheBatchForms()
    {
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        await CompileAsync("TC-102");
        _ws.WriteVerdict("TC-102", "grounded");
        EditByHand("TC-101");

        var (_, verdicts, _) = await _ws.IngestVerdictAllAsync();
        var classified = BaselineTestWorkspace.Json(verdicts);

        Assert.Equal(1, classified.GetProperty("skipped_stale").GetInt32());
        Assert.Equal(["TC-101"], classified.GetProperty("stale").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, classified.GetProperty("grounded").GetInt32());

        var (_, grounding, _) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");
        var written = BaselineTestWorkspace.Json(grounding);

        Assert.Equal(1, written.GetProperty("skipped_stale").GetInt32());
        Assert.Equal(["TC-101"], written.GetProperty("stale").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, written.GetProperty("written").GetInt32());
        Assert.Null(_ws.GroundingOf("storage", "TC-101"));
        Assert.NotNull(_ws.GroundingOf("storage", "TC-102"));
    }

    [Fact]
    public async Task AVerdictWithoutARecord_IsAcceptedAsBefore()
    {
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        File.Delete(_ws.VerdictRecordPath("TC-101"));
        EditByHand("TC-101");

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.True(exit == 0, error);
        Assert.NotNull(_ws.GroundingOf("storage", "TC-101"));

        // A verdict file outside .spectra/verdicts/ has no record either.
        var elsewhere = _ws.PathOf(".spectra/other-verdict.json");
        File.Copy(_ws.VerdictPath("TC-101"), elsewhere);
        var (verdictExit, _, verdictError) = await _ws.RunAsync("ai", "ingest-verdict", "--from", elsewhere, "--output-format", "json");
        Assert.True(verdictExit == 0, verdictError);
    }

    [Fact]
    public async Task Pre090Workspace_VerdictsWithoutRecords_AndExistingBlocks_BehaveAsBefore()
    {
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        await _ws.InitBaselineAsync();
        await _ws.WriteCriticGroundingAsync("storage", "TC-102");
        _ws.WriteVerdict("TC-102", "grounded");
        _ws.WriteVerdict("TC-103", "grounded"); // no record: written before 090

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var batch = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, batch.GetProperty("written").GetInt32());
        Assert.Equal(1, batch.GetProperty("skipped_already_written").GetInt32());
        Assert.Equal(0, batch.GetProperty("skipped_stale").GetInt32());
        Assert.NotNull(_ws.GroundingOf("storage", "TC-103"));
    }
}
