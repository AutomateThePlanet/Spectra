using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>Spec 084 T5 (FR-026, FR-026a, FR-028): <c>spectra ai source-baseline --init</c>.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SourceBaselineCommandTests : IDisposable
{
    private const string StorageDoc = "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Archive tier\n\nOld files move after 90 days.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public SourceBaselineCommandTests()
    {
        _ws.WriteDoc("docs/storage.md", StorageDoc);
        _ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-001", "Each user MUST get 10GB"));
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], ["AC-STORAGE-001"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas", "docs/storage.md#Archive-tier"]);
    }

    public void Dispose() => _ws.Dispose();

    private static JsonElement Suite(JsonElement result, string suite = "storage") =>
        result.GetProperty("suites").EnumerateArray().Single(s => s.GetProperty("suite").GetString() == suite);

    [Fact]
    public async Task Init_CreatesSortedEntries_AndOneSnapshotPerSection()
    {
        var tests = _ws.TestFiles();

        var result = await _ws.InitBaselineAsync();

        Assert.Equal(2, Suite(result).GetProperty("created").GetInt32());
        Assert.Equal(2, result.GetProperty("snapshots_written").GetInt32());
        var baseline = _ws.BaselineJson("storage");
        Assert.Equal(1, baseline.GetProperty("version").GetInt32());
        var tc101 = baseline.GetProperty("tests").GetProperty("TC-101");
        Assert.StartsWith("sha256:", tc101.GetProperty("refs").GetProperty("docs/storage.md#Quotas").GetString());
        Assert.StartsWith("sha256:", tc101.GetProperty("criteria").GetProperty("AC-STORAGE-001").GetString());
        Assert.Equal(["docs/storage.md#Archive-tier", "docs/storage.md#Quotas"],
            baseline.GetProperty("tests").GetProperty("TC-102").GetProperty("refs").EnumerateObject().Select(p => p.Name));
        Assert.Equal(2, _ws.SnapshotFiles().Length);
        Assert.Equal(tests, _ws.TestFiles());
    }

    [Fact]
    public async Task SecondRun_IsUnchanged_AndByteIdentical()
    {
        await _ws.InitBaselineAsync();
        var first = _ws.ReadBaseline("storage");

        var result = await _ws.InitBaselineAsync();

        Assert.Equal(2, Suite(result).GetProperty("unchanged").GetInt32());
        Assert.Equal(0, result.GetProperty("snapshots_written").GetInt32());
        Assert.Equal(first, _ws.ReadBaseline("storage"));
    }

    [Fact]
    public async Task PartialEntry_IsCompleted_WithoutChangingExistingHashes()
    {
        await _ws.InitBaselineAsync();
        var before = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs").GetProperty("docs/storage.md#Quotas").GetString();
        _ws.WriteDoc("docs/storage.md", StorageDoc.Replace("10GB", "50GB"));
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Archive-tier"], ["AC-STORAGE-001"]);

        var result = await _ws.InitBaselineAsync();

        Assert.Equal(1, Suite(result).GetProperty("completed").GetInt32());
        var refs = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.Equal(before, refs.GetProperty("docs/storage.md#Quotas").GetString());
        Assert.True(refs.TryGetProperty("docs/storage.md#Archive-tier", out _));
    }

    [Fact]
    public async Task ExistingStaleHash_IsKeptWithoutForce_AndRecomputedWithForce()
    {
        await _ws.InitBaselineAsync();
        var before = _ws.ReadBaseline("storage");
        _ws.WriteDoc("docs/storage.md", StorageDoc.Replace("10GB", "50GB"));

        var keep = await _ws.InitBaselineAsync();
        Assert.Equal(before, _ws.ReadBaseline("storage"));
        Assert.Equal(0, Suite(keep).GetProperty("recomputed").GetInt32());

        var force = await _ws.InitBaselineAsync("storage", "--force");
        Assert.Equal(2, Suite(force).GetProperty("recomputed").GetInt32());
        Assert.NotEqual(before, _ws.ReadBaseline("storage"));
    }

    [Fact]
    public async Task StaleKey_IsKeptByInit_AndDroppedByForce()
    {
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]); // Archive-tier removed by hand

        await _ws.InitBaselineAsync();
        Assert.True(_ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-102").GetProperty("refs").TryGetProperty("docs/storage.md#Archive-tier", out _));

        await _ws.InitBaselineAsync("storage", "--force");
        Assert.False(_ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-102").GetProperty("refs").TryGetProperty("docs/storage.md#Archive-tier", out _));
    }

    [Fact]
    public async Task AllSuites_CoversEverySuite()
    {
        _ws.WriteTest("billing", "TC-201", ["docs/storage.md#Quotas"]);

        var result = await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");

        Assert.Equal(["billing", "storage"], result.GetProperty("suites").EnumerateArray().Select(s => s.GetProperty("suite").GetString()));
        Assert.NotNull(_ws.ReadBaseline("billing"));
        Assert.NotNull(_ws.ReadBaseline("storage"));
    }

    [Fact]
    public async Task DryRun_WritesNothing_AndReportsTheCounts()
    {
        var before = _ws.Snapshot();

        var result = await _ws.InitBaselineAsync("storage", "--dry-run");

        Assert.Equal(before, _ws.Snapshot());
        Assert.True(result.GetProperty("dry_run").GetBoolean());
        Assert.Equal(2, Suite(result).GetProperty("created").GetInt32());
        Assert.Equal(2, result.GetProperty("snapshots_written").GetInt32());
    }

    [Fact]
    public async Task UnresolvedRef_AndUnknownCriterion_AreOmitted()
    {
        _ws.WriteTest("storage", "TC-103", ["docs/missing.md#X", "docs/storage.md#NoSuchHeading", "docs/storage.md#Quotas"], ["AC-NOPE-999"]);

        await _ws.InitBaselineAsync();

        var entry = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-103");
        Assert.Equal(["docs/storage.md#Quotas"], entry.GetProperty("refs").EnumerateObject().Select(p => p.Name));
        Assert.Empty(entry.GetProperty("criteria").EnumerateObject());
    }

    [Fact]
    public async Task EmptySuite_CreatesNoBaselineFile()
    {
        Directory.CreateDirectory(_ws.PathOf("test-cases/empty"));

        var result = await _ws.InitBaselineAsync("empty");

        Assert.Equal(0, Suite(result, "empty").GetProperty("created").GetInt32());
        Assert.Null(_ws.ReadBaseline("empty"));
    }

    [Fact]
    public async Task HumanSummary_IsOnStderr_InJsonMode()
    {
        var (exit, output, error) = await _ws.RunAsync("ai", "source-baseline", "--init", "--suite", "storage", "--output-format", "json");

        Assert.Equal(0, exit);
        BaselineTestWorkspace.Json(output); // stdout is exactly one JSON document
        Assert.Contains("Source baseline — storage: 2 created", error);
        Assert.Equal("source-baseline", BaselineTestWorkspace.Json(output).GetProperty("command").GetString());
    }

    [Fact]
    public async Task HumanMode_PrintsTheSummaryOnStdout()
    {
        var (exit, output, _) = await _ws.RunAsync("ai", "source-baseline", "--init", "--suite", "storage");

        Assert.Equal(0, exit);
        Assert.Contains("Source baseline — storage: 2 created", output);
    }
}
