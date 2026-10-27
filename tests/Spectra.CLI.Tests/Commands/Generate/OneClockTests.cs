using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 097 F4 (FR-013–FR-015; SC-004): freshness compares two file-system times — the verdict file's and the
/// fingerprint record file's — so a CLI clock that differs from the file system's (a network share, a container mount)
/// no longer refuses new verdicts. <c>compiled_at</c> is skewed by ±1 h here and must not decide. On 807047c it did.
/// 090 records (no <c>compiled_at</c>) keep the fingerprint-only rule; the 2 s allowance stays.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class OneClockTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);
    private readonly DateTime _t = DateTime.UtcNow.AddHours(-3);

    public OneClockTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.TwoSuites();
        var (code, _, err) = _ws.CompileCriticAsync("a", "TC-102").GetAwaiter().GetResult();
        Assert.True(code == 0, err);
        _ws.WriteVerdict("TC-102", "grounded");
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>Record file time t; verdict file time t + <paramref name="verdictOffset"/>; compiled_at t + <paramref name="compiledOffset"/>.</summary>
    private void Times(TimeSpan verdictOffset, TimeSpan? compiledOffset)
    {
        if (compiledOffset is { } offset)
        {
            _ws.SetCompiledAt("TC-102", _t + offset);
        }

        VerificationWorkspace.SetFileTime(_ws.VerdictRecordPath("TC-102"), _t);
        VerificationWorkspace.SetFileTime(_ws.VerdictPath("TC-102"), _t + verdictOffset);
    }

    private Task<(int Exit, string Out, string Err)> IngestVerdictAsync() =>
        _ws.RunAsync("ai", "ingest-verdict", "--from", _ws.VerdictPath("TC-102"), "--output-format", "json");

    [Fact]
    public async Task AVerdictAfterTheRecord_IsFresh_EvenWhenCompiledAtIsAnHourAhead()
    {
        Times(TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));

        var (exit, _, error) = await IngestVerdictAsync();
        Assert.True(exit == 0, error);

        var (groundExit, _, groundError) = await _ws.IngestGroundingAsync("--suite", "a", "--test", "TC-102");
        Assert.True(groundExit == 0, groundError);
        Assert.NotNull(_ws.GroundingOf("a", "TC-102"));
    }

    [Fact]
    public async Task AVerdictBeforeTheRecord_IsStale_EvenWhenCompiledAtIsAnHourBehind()
    {
        Times(TimeSpan.FromSeconds(-10), TimeSpan.FromHours(-1));

        var (exit, _, error) = await IngestVerdictAsync();

        Assert.Equal(4, exit);
        Assert.Contains("VERDICT_STALE", error);
    }

    [Fact]
    public async Task OneSecondBeforeTheRecord_IsWithinTheAllowance()
    {
        Times(TimeSpan.FromSeconds(-1), TimeSpan.Zero);

        var (exit, _, error) = await IngestVerdictAsync();

        Assert.True(exit == 0, error);
    }

    [Fact]
    public async Task A090Record_KeepsTheFingerprintRule()
    {
        var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_ws.VerdictRecordPath("TC-102")))!.AsObject();
        record.Remove("compiled_at");
        File.WriteAllText(_ws.VerdictRecordPath("TC-102"), record.ToJsonString());
        Times(TimeSpan.FromHours(-1), null);

        var (exit, _, error) = await IngestVerdictAsync();

        Assert.True(exit == 0, error);
    }

    [Fact]
    public async Task EveryCompile_MovesTheRecordsFileTime()
    {
        VerificationWorkspace.SetFileTime(_ws.VerdictRecordPath("TC-102"), _t);

        var (code, _, err) = await _ws.CompileCriticAsync("a", "TC-102");

        Assert.True(code == 0, err);
        Assert.True(VerificationWorkspace.FileTime(_ws.VerdictRecordPath("TC-102")) > _t.AddHours(1));
    }
}
