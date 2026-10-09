using System.Security.Cryptography;
using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Generation;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.IdAllocation;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 090 T7 (FR-B2): <c>ingest-update --dry-run</c> reports the outcome every edit would get, with the same exit code
/// as the real run, and changes nothing — no test, index, baseline, staging or verdict file — and takes no lock.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestUpdateDryRunTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";
    private const string Sharing = "docs/storage.md#Sharing";

    private readonly BaselineTestWorkspace _ws = new();

    public IngestUpdateDryRunTests()
    {
        _ws.WriteDoc("docs/storage.md",
            "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n\n## Sharing\n\nFiles can be shared by link.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103", "TC-104", "TC-105" })
        {
            _ws.WriteTest("storage", id, [id == "TC-102" ? Sharing : Quotas]);
        }

        _ws.WriteTest("storage", "TC-106", [Sharing]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md",
            "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n\n## Sharing\n\nFiles can be shared by link.\n");
    }

    public void Dispose()
    {
        BaselineStore.LockTimeoutOverride = null;
        _ws.Dispose();
    }

    private TestCase Load(string id) => _ws.Load("storage", id);

    private static string Array(params TestCase[] tests) =>
        "[\n" + string.Join(",\n", tests.Select(UpdatePromptCompiler.SerializeTest)) + "\n]\n";

    /// <summary>One staged file per outcome: written, unchanged, unchanged_pending, and four failures.</summary>
    private void StageEveryOutcome()
    {
        _ws.StageEdit("storage", "TC-101", Load("TC-101") with { ExpectedResult = "Each user gets 45GB" }); // written
        _ws.StageEdit("storage", "TC-102", Load("TC-102"));                                                  // unchanged (Sharing unchanged)
        _ws.StageEdit("storage", "TC-103", Load("TC-103"));                                                  // unchanged_pending (LOAD)
        _ws.StageRaw("storage", "TC-104", Array(Load("TC-104"), Load("TC-105")));                            // MULTIPLE_TESTS
        _ws.StageEdit("storage", "TC-105", Load("TC-106"));                                                  // ID_MISMATCH
        _ws.StageEdit("storage", "TC-106", Load("TC-106") with { Priority = Priority.Low });                 // DRIFT_DETECTED
        _ws.StageEdit("storage", "TC-999", Load("TC-101") with { Id = "TC-999" });                           // NO_ORIGINAL
    }

    private static string Results(string output) =>
        string.Join("\n", BaselineTestWorkspace.Json(output).GetProperty("results").EnumerateArray()
            .Select(r => string.Join("|", r.GetProperty("test").GetString(), r.GetProperty("outcome").GetString(),
                r.TryGetProperty("error_code", out var c) ? c.GetString() : "",
                r.TryGetProperty("bucket", out var b) ? b.GetString() : "")));

    private Dictionary<string, string> HashTree() =>
        Directory.EnumerateFiles(_ws.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(_ws.Root, f), f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    [Fact]
    public async Task TheBatchDryRun_EqualsTheRealRun_AndChangesNothing()
    {
        StageEveryOutcome();
        var before = HashTree();

        var (dryExit, dryOutput, dryError) = await _ws.IngestAllAsync("storage", "--dry-run");

        Assert.Equal(before, HashTree());
        Assert.True(BaselineTestWorkspace.Json(dryOutput).GetProperty("dry_run").GetBoolean());
        Assert.Contains("Dry run: nothing written.", dryError);

        var (realExit, realOutput, _) = await _ws.IngestAllAsync();

        Assert.Equal(8, realExit);
        Assert.Equal(realExit, dryExit);
        Assert.Equal(Results(realOutput), Results(dryOutput));
        Assert.Contains("written|", Results(dryOutput));
        Assert.Contains("TC-103|unchanged_pending||LOAD", Results(dryOutput));
        Assert.Contains("TC-104|failed|MULTIPLE_TESTS|", Results(dryOutput));
        Assert.Contains("TC-105|failed|ID_MISMATCH|", Results(dryOutput));
        Assert.Contains("TC-106|failed|DRIFT_DETECTED|", Results(dryOutput));
        Assert.Contains("TC-999|failed|NO_ORIGINAL|", Results(dryOutput));
    }

    [Fact]
    public async Task TheRepairsFolderDryRun_EqualsTheRealRun()
    {
        _ws.StageRepair("storage", "TC-101", Load("TC-101") with { ExpectedResult = "Repaired" });
        _ws.StageRepairRaw("storage", "TC-102", "[ { \"id\": \"TC-102\", \"title\": } ]");
        var before = HashTree();

        var (dryExit, dryOutput, _) = await _ws.IngestAllAsync("storage", "--keep-baseline", "--dry-run");

        Assert.Equal(before, HashTree());
        var (realExit, realOutput, _) = await _ws.IngestAllAsync("storage", "--keep-baseline");
        Assert.Equal(realExit, dryExit);
        Assert.Equal(Results(realOutput), Results(dryOutput));
    }

    [Theory]
    [InlineData("TC-101", "written")]
    [InlineData("TC-102", "unchanged")]
    [InlineData("TC-103", "unchanged_pending")]
    public async Task TheSingleFormDryRun_EqualsTheRealRun_AndChangesNothing(string id, string outcome)
    {
        var test = id == "TC-101" ? Load(id) with { ExpectedResult = "Each user gets 45GB" } : Load(id);
        var from = _ws.PathOf($".spectra/edit-{id}.json");
        File.WriteAllText(from, Array(test));
        var before = HashTree();

        var (dryExit, dryOutput, _) = await _ws.RunAsync(
            "ai", "ingest-update", "storage", "--test-id", id, "--from", from, "--dry-run", "--output-format", "json");

        Assert.Equal(before, HashTree());
        var dry = BaselineTestWorkspace.Json(dryOutput);
        Assert.True(dry.GetProperty("dry_run").GetBoolean());
        Assert.Equal(outcome, dry.GetProperty("outcome").GetString());

        var (realExit, realOutput, _) = await _ws.RunAsync(
            "ai", "ingest-update", "storage", "--test-id", id, "--from", from, "--output-format", "json");
        Assert.Equal(realExit, dryExit);
        Assert.Equal(outcome, BaselineTestWorkspace.Json(realOutput).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task AnUnreadableBaseline_FailsTheDryRunLikeTheRealRun()
    {
        StageEveryOutcome();
        File.WriteAllText(_ws.BaselinePath("storage"), "{ not json");

        var (dryExit, _, dryError) = await _ws.IngestAllAsync("storage", "--dry-run");
        var (realExit, _, _) = await _ws.IngestAllAsync();

        Assert.Equal(1, dryExit);
        Assert.Equal(realExit, dryExit);
        Assert.Equal("BASELINE_UNREADABLE", BaselineTestWorkspace.Json(dryError).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task TheDryRun_TakesNoLock()
    {
        // Analysis U2: with the baseline lock held, the dry run still completes; the real run times out.
        StageEveryOutcome();
        BaselineStore.LockTimeoutOverride = TimeSpan.FromMilliseconds(200);
        using var held = await FileLockHandle.AcquireAsync(_ws.PathOf(".spectra/baseline.lock"), TimeSpan.FromSeconds(5));

        var (dryExit, dryOutput, dryError) = await _ws.IngestAllAsync("storage", "--dry-run");

        Assert.True(dryExit == 8, dryError);
        Assert.NotEmpty(BaselineTestWorkspace.Json(dryOutput).GetProperty("results").EnumerateArray());

        var (realExit, _, realError) = await _ws.IngestAllAsync();

        Assert.Equal(1, realExit);
        Assert.Equal("LOCK_TIMEOUT", BaselineTestWorkspace.Json(realError).GetProperty("error_code").GetString());
    }
}
