using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 097 F3 (FR-010–FR-012): a stale verdict never drives a repair. <c>compile-repair-prompt</c> refuses it with
/// <c>VERDICT_STALE</c> (exit 4, the ingest message); <c>compile-repair-batch</c> leaves it out of its stdout array and
/// lists it on stderr under <c>stale</c>. A verdict without a record is accepted, as before. On 807047c both commands
/// compiled a repair prompt from a stale verdict.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class RepairFreshnessTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public RepairFreshnessTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.TwoSuites();
    }

    public void Dispose() => _ws.Dispose();

    private async Task PartialVerdictAsync(string id)
    {
        var (code, _, err) = await _ws.CompileCriticAsync("a", id);
        Assert.True(code == 0, err);
        _ws.WriteVerdict(id, "partial");
    }

    private Task<(int Exit, string Out, string Err)> RepairPromptAsync(string id) =>
        _ws.RunAsync("ai", "compile-repair-prompt", "--suite", "a", "--test", id);

    [Fact]
    public async Task ThePrompt_RefusesAVerdictGivenToOtherContent()
    {
        await PartialVerdictAsync("TC-102");
        _ws.EditByHand("a", "TC-102");

        var (exit, output, error) = await RepairPromptAsync("TC-102");

        Assert.Equal(4, exit);
        Assert.StartsWith("VERDICT_STALE:", error.TrimStart());
        Assert.Contains("run the critic for TC-102 again", error);
        Assert.Equal("", output.Trim());
    }

    [Fact]
    public async Task ThePrompt_RefusesAVerdictOlderThanTheLastCriticPrompt()
    {
        await PartialVerdictAsync("TC-102");
        VerificationWorkspace.SetFileTime(_ws.VerdictPath("TC-102"),
            VerificationWorkspace.FileTime(_ws.VerdictRecordPath("TC-102")).AddHours(-1));

        var (exit, _, error) = await RepairPromptAsync("TC-102");

        Assert.Equal(4, exit);
        Assert.Contains("VERDICT_STALE", error);
    }

    [Fact]
    public async Task ThePrompt_AcceptsAVerdictWithoutARecord()
    {
        _ws.WriteVerdict("TC-102", "partial");

        var (exit, output, error) = await RepairPromptAsync("TC-102");

        Assert.True(exit == 0, error);
        Assert.NotEqual("", output.Trim());
    }

    [Fact]
    public async Task TheBatch_LeavesStaleTestsOut_AndListsThemOnStderr()
    {
        await PartialVerdictAsync("TC-102");
        await PartialVerdictAsync("TC-104");
        _ws.EditByHand("a", "TC-102");

        var (exit, output, error) = await _ws.RunAsync("ai", "compile-repair-batch", "--suite", "a");

        Assert.True(exit == 0, error);
        var ids = JsonDocument.Parse(output).RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToList();
        Assert.Equal(["TC-104"], ids);
        var line = error.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith("{\"command\":\"compile-repair-batch\"", StringComparison.Ordinal));
        var stale = VerdictWorkspace.Json(line).GetProperty("stale").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToList();
        Assert.Equal(["TC-102"], stale);
    }
}
