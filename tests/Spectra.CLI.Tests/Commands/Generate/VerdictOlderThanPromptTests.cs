using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 091 T1–T3 (F1, FR-001–FR-005): a verdict written before the last critic prompt for its test is stale, even when
/// the recorded fingerprint matches the test's current content (the critic ran again but wrote no verdict). A verdict
/// written after the compile is fresh even when byte-identical to the old one; a 090 record (no <c>compiled_at</c>) keeps
/// the fingerprint-only rule. On 5c10c44 the old verdict passed as fresh. Workspace from the real <c>spectra init</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class VerdictOlderThanPromptTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public VerdictOlderThanPromptTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        _ws.WriteTest("storage", "TC-102", [Quotas]);
    }

    public void Dispose() => _ws.Dispose();

    private async Task CompileAsync(string id)
    {
        var (exit, _, error) = await _ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
    }

    private void EditByHand(string id) =>
        File.WriteAllText(_ws.TestPath("storage", id), File.ReadAllText(_ws.TestPath("storage", id)).Replace("It works", "It still works"));

    private void SetVerdictTime(string id, DateTime utc) => File.SetLastWriteTimeUtc(_ws.VerdictPath(id), utc);

    /// <summary>A 090-format record: the same JSON without <c>compiled_at</c>.</summary>
    private void StripCompiledAt(string id)
    {
        var record = JsonNode.Parse(File.ReadAllText(_ws.VerdictRecordPath(id)))!.AsObject();
        record.Remove("compiled_at");
        File.WriteAllText(_ws.VerdictRecordPath(id), record.ToJsonString());
    }

    /// <summary>Verdict, then a hand edit, then the critic prompt compiled again — and no new verdict.</summary>
    private async Task OldVerdictAfterAFailedCriticRunAsync(string id = "TC-101")
    {
        await CompileAsync(id);
        _ws.WriteVerdict(id, "grounded");
        SetVerdictTime(id, DateTime.UtcNow.AddHours(-1));
        EditByHand(id);
        await CompileAsync(id);
    }

    private static List<string> Ids(JsonElement result, string property) =>
        result.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToList();

    // ---- T1 (fails on 5c10c44) ----

    [Fact]
    public async Task IngestVerdictFrom_RefusesAVerdictOlderThanThePrompt()
    {
        await OldVerdictAfterAFailedCriticRunAsync();

        var (exit, _, error) = await _ws.RunAsync("ai", "ingest-verdict", "--from", _ws.VerdictPath("TC-101"), "--output-format", "json");

        Assert.Equal(4, exit);
        var failure = BaselineTestWorkspace.Json(error);
        Assert.Equal("VERDICT_STALE", failure.GetProperty("error_code").GetString());
        Assert.Contains("older than the last critic prompt", failure.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task IngestVerdictAll_ListsAVerdictOlderThanThePromptAsStale()
    {
        await OldVerdictAfterAFailedCriticRunAsync();

        var (exit, output, error) = await _ws.IngestVerdictAllAsync();

        Assert.True(exit == 0, error);
        Assert.Contains("TC-101", Ids(BaselineTestWorkspace.Json(output), "stale"));
    }

    [Fact]
    public async Task IngestGroundingTest_RefusesAVerdictOlderThanThePrompt()
    {
        await OldVerdictAfterAFailedCriticRunAsync();

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.Equal(4, exit);
        Assert.Equal("VERDICT_STALE", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
        Assert.Null(_ws.GroundingOf("storage", "TC-101"));
    }

    [Fact]
    public async Task IngestGroundingAll_ListsAVerdictOlderThanThePromptAsStale_AndWritesNothing()
    {
        await OldVerdictAfterAFailedCriticRunAsync();

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Contains("TC-101", Ids(result, "stale"));
        Assert.Equal(0, result.GetProperty("written").GetInt32());
        Assert.Null(_ws.GroundingOf("storage", "TC-101"));
    }

    // ---- T1b (fails on 5c10c44) ----

    [Fact]
    public async Task CompileCriticPrompt_RecordsWhenItCompiled_InUtc()
    {
        var before = DateTimeOffset.UtcNow;
        await CompileAsync("TC-101");
        var after = DateTimeOffset.UtcNow;

        var record = BaselineTestWorkspace.Json(File.ReadAllText(_ws.VerdictRecordPath("TC-101")));
        Assert.True(record.TryGetProperty("compiled_at", out var compiledAt), "the record has no compiled_at");
        var at = DateTimeOffset.Parse(compiledAt.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(TimeSpan.Zero, at.Offset);
        Assert.InRange(at, before, after);
    }

    // ---- T2 (guard: passes on 5c10c44 and after) ----

    [Fact]
    public async Task AByteIdenticalVerdictWrittenAfterTheCompile_IsFresh()
    {
        await OldVerdictAfterAFailedCriticRunAsync();
        var bytes = File.ReadAllBytes(_ws.VerdictPath("TC-101"));

        File.WriteAllBytes(_ws.VerdictPath("TC-101"), bytes);
        SetVerdictTime("TC-101", DateTime.UtcNow.AddMinutes(1));
        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        Assert.Equal(1, BaselineTestWorkspace.Json(output).GetProperty("written").GetInt32());
        Assert.NotNull(_ws.GroundingOf("storage", "TC-101"));
    }

    // ---- T3 (guard: a 090 record keeps the fingerprint-only rule) ----

    [Fact]
    public async Task A090Record_WithMatchingContent_IsAcceptedByAllFourCommands_HoweverOldTheVerdict()
    {
        foreach (var id in new[] { "TC-101", "TC-102" })
        {
            await CompileAsync(id);
            StripCompiledAt(id);
            _ws.WriteVerdict(id, "grounded");
            SetVerdictTime(id, DateTime.UtcNow.AddHours(-1));
        }

        var (fromExit, _, fromError) = await _ws.RunAsync("ai", "ingest-verdict", "--from", _ws.VerdictPath("TC-101"), "--output-format", "json");
        Assert.True(fromExit == 0, fromError);

        var (allExit, allOutput, allError) = await _ws.IngestVerdictAllAsync();
        Assert.True(allExit == 0, allError);
        var verdicts = BaselineTestWorkspace.Json(allOutput);
        Assert.Empty(Ids(verdicts, "stale"));
        Assert.Equal(2, verdicts.GetProperty("grounded").GetInt32());

        var (testExit, _, testError) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");
        Assert.True(testExit == 0, testError);
        Assert.NotNull(_ws.GroundingOf("storage", "TC-101"));

        var (batchExit, batchOutput, batchError) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");
        Assert.True(batchExit == 0, batchError);
        var batch = BaselineTestWorkspace.Json(batchOutput);
        Assert.Empty(Ids(batch, "stale"));
        Assert.Equal(1, batch.GetProperty("written").GetInt32());
        Assert.NotNull(_ws.GroundingOf("storage", "TC-102"));
    }

    // ---- T007 (US1): the 2 s allowance, the file actually read, stdin, a pre-091 workspace ----

    private DateTime CompiledAt(string id) =>
        DateTimeOffset.Parse(
            BaselineTestWorkspace.Json(File.ReadAllText(_ws.VerdictRecordPath(id))).GetProperty("compiled_at").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture).UtcDateTime;

    [Theory]
    [InlineData(-3.0, true)]
    [InlineData(-1.0, false)]
    [InlineData(0.0, false)]
    [InlineData(60.0, false)]
    public async Task TheVerdictTime_IsComparedWithTheCompileTime_WithATwoSecondAllowance(double seconds, bool stale)
    {
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        SetVerdictTime("TC-101", CompiledAt("TC-101").AddSeconds(seconds));

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        if (stale)
        {
            Assert.Equal(4, exit);
            Assert.Equal("VERDICT_STALE", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
            Assert.Null(_ws.GroundingOf("storage", "TC-101"));
        }
        else
        {
            Assert.True(exit == 0, error);
            Assert.NotNull(_ws.GroundingOf("storage", "TC-101"));
        }
    }

    [Fact]
    public async Task IngestGroundingTestFrom_ChecksTheFileActuallyRead()
    {
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        var copy = _ws.PathOf(".spectra/verdict-copy.json");
        File.Copy(_ws.VerdictPath("TC-101"), copy);
        File.SetLastWriteTimeUtc(copy, CompiledAt("TC-101").AddHours(-1));

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101", "--from", copy);

        Assert.Equal(4, exit);
        Assert.Equal("VERDICT_STALE", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
        Assert.Null(_ws.GroundingOf("storage", "TC-101"));
    }

    [Fact]
    public async Task IngestVerdictFromStdin_HasNoFileTime_AndIsAccepted()
    {
        await CompileAsync("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        var json = File.ReadAllText(_ws.VerdictPath("TC-101"));
        SetVerdictTime("TC-101", DateTime.UtcNow.AddHours(-1));

        var originalIn = Console.In;
        try
        {
            Console.SetIn(new StringReader(json));
            var (exit, output, error) = await _ws.RunAsync("ai", "ingest-verdict", "--output-format", "json");

            Assert.True(exit == 0, error);
            Assert.Equal("grounded", BaselineTestWorkspace.Json(output).GetProperty("verdict").GetString());
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    [Fact]
    public async Task APre091Workspace_RunsAsBefore()
    {
        // FR-015: 090 records (no compiled_at), verdict files and a critic grounding block on another test.
        foreach (var id in new[] { "TC-101", "TC-102" })
        {
            await CompileAsync(id);
            StripCompiledAt(id);
            _ws.WriteVerdict(id, "grounded");
            SetVerdictTime(id, DateTime.UtcNow.AddHours(-1));
        }

        await _ws.WriteCriticGroundingAsync("storage", "TC-102");
        // TC-102's record still matches: a grounding block is not part of the fingerprint.

        var (fromExit, _, fromError) = await _ws.RunAsync("ai", "ingest-verdict", "--from", _ws.VerdictPath("TC-101"), "--output-format", "json");
        Assert.True(fromExit == 0, fromError);
        var (verdictExit, verdictOutput, verdictError) = await _ws.IngestVerdictAllAsync();
        Assert.True(verdictExit == 0, verdictError);
        Assert.Empty(Ids(BaselineTestWorkspace.Json(verdictOutput), "stale"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--all");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, result.GetProperty("written").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped_already_written").GetInt32());
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
        var (auditExit, _, auditError) = await _ws.AuditGroundingJsonAsync();
        Assert.True(auditExit == 0, auditError);
    }

    [Fact]
    public async Task A090Record_WithChangedContent_IsStale()
    {
        await CompileAsync("TC-101");
        StripCompiledAt("TC-101");
        _ws.WriteVerdict("TC-101", "grounded");
        EditByHand("TC-101");

        var (exit, _, error) = await _ws.IngestGroundingAsync("--suite", "storage", "--test", "TC-101");

        Assert.Equal(4, exit);
        Assert.Equal("VERDICT_STALE", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
    }
}
