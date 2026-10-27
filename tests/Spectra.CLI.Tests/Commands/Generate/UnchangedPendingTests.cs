using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 089 T3 (F3, FR-009–FR-015): an edit returned identical to the original only counts as reviewed when nothing that
/// needs an edit is pending. For LOAD, CRITERIA_CHANGED, REMOVED, CRITERIA_REMOVED and RELINK the outcome is
/// <c>unchanged_pending</c>: nothing is recorded, so the change stays in triage. UNCHANGED, COSMETIC and LIGHT are
/// <c>unchanged</c> and recorded, as in 088. On 6ea7520 every identical edit rebuilt the baseline.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UnchangedPendingTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";
    private const string Sharing = "docs/storage.md#Sharing";
    private const string SharingText = "Files can be shared by link with any registered user of the workspace.";
    private const string Pending = "returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again)";

    private static string Doc(string quota = "30GB", string quotasHeading = "Quotas", string sharing = SharingText) =>
        $"# Storage\n\n## {quotasHeading}\n\nEach user gets **{quota}** of storage space.\n\n## Sharing\n\n{sharing}\n";

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private void Criteria(string ac1 = "Each user MUST get 30GB", bool withAc2 = true)
    {
        if (withAc2)
        {
            _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", ac1), ("AC-2", "Files MAY be shared by link"));
        }
        else
        {
            _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", ac1));
        }
    }

    private void StageIdentical(string id) => _ws.StageEdit("storage", id, _ws.Load("storage", id));

    private string TestFile(string id) => File.ReadAllText(_ws.PathOf($"test-cases/storage/{id}.md"));

    private static JsonElement Result(JsonElement batch, string id) =>
        batch.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("test").GetString() == id);

    private async Task<string?> BucketAsync(string id, BaselineTestWorkspace? ws = null)
    {
        var group = BaselineTestWorkspace.GroupOf(await (ws ?? _ws).TriageAsync("storage", "--dry-run"), id);
        return group.ValueKind == JsonValueKind.Undefined ? null : group.GetProperty("bucket").GetString();
    }

    private static string? Entry(BaselineTestWorkspace ws, string id) =>
        ws.BaselineJson("storage").GetProperty("tests").TryGetProperty(id, out var entry) ? entry.GetRawText() : null;

    /// <summary>One test, baselined, then one change. Returns the bucket triage gives it.</summary>
    private async Task<string> ArrangeAsync(string change)
    {
        _ws.WriteDoc("docs/storage.md", Doc());
        Criteria();
        switch (change)
        {
            case "LOAD":
            case "REMOVED":
                _ws.WriteTest("storage", "TC-101", [Quotas]);
                break;
            case "CRITERIA_CHANGED":
                _ws.WriteTest("storage", "TC-101", [Sharing], ["AC-1"]);
                break;
            case "CRITERIA_REMOVED":
                _ws.WriteTest("storage", "TC-101", [Sharing], ["AC-2"]);
                break;
            default:
                _ws.WriteTest("storage", "TC-101", [Sharing]);
                break;
        }

        await _ws.InitBaselineAsync();
        switch (change)
        {
            case "LOAD": _ws.WriteDoc("docs/storage.md", Doc(quota: "45GB")); break;
            case "REMOVED": _ws.WriteDoc("docs/storage.md", Doc(quotasHeading: "Limits")); break;
            case "CRITERIA_CHANGED": Criteria(ac1: "Each user MUST get 30GB of space after signing in"); break;
            case "CRITERIA_REMOVED": Criteria(withAc2: false); break;
            case "LIGHT": _ws.WriteDoc("docs/storage.md", Doc(sharing: SharingText.Replace("by link", "through a link"))); break;
            case "COSMETIC": _ws.WriteDoc("docs/storage.md", Doc(sharing: SharingText.Replace("Files", "**Files**"))); break;
        }

        // COSMETIC and UNCHANGED tests form no groups; COSMETIC shows only in the suite's counts.
        var triage = await _ws.TriageAsync("storage", "--no-auto-ack", "--dry-run");
        var group = BaselineTestWorkspace.GroupOf(triage, "TC-101");
        if (group.ValueKind != JsonValueKind.Undefined)
        {
            return group.GetProperty("bucket").GetString()!;
        }

        return BaselineTestWorkspace.Count(triage, "storage", "cosmetic") > 0 ? "COSMETIC" : "UNCHANGED";
    }

    [Theory]
    [InlineData("LOAD")]
    [InlineData("CRITERIA_CHANGED")]
    [InlineData("REMOVED")]
    [InlineData("CRITERIA_REMOVED")]
    public async Task ABucketNeedingAnEdit_ReturnedUnchanged_IsPending_AndStaysInTriage(string bucket)
    {
        Assert.Equal(bucket, await ArrangeAsync(bucket));
        StageIdentical("TC-101");
        var fileBefore = TestFile("TC-101");
        var baselineBefore = _ws.ReadBaseline("storage");

        var batch = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        var result = Result(batch, "TC-101");
        Assert.Equal("unchanged_pending", result.GetProperty("outcome").GetString());
        Assert.Equal(bucket, result.GetProperty("bucket").GetString());
        Assert.NotEmpty(result.GetProperty("why").EnumerateArray());
        Assert.Equal(1, batch.GetProperty("unchanged_pending").GetInt32());
        Assert.Equal(0, batch.GetProperty("unchanged").GetInt32());
        Assert.Equal(fileBefore, TestFile("TC-101"));
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.Empty(_ws.StagedFiles("storage"));
        Assert.Equal(bucket, await BucketAsync("TC-101"));
    }

    [GitFact]
    public async Task Relink_ReturnedUnchanged_IsPending_AndStaysInTriage()
    {
        _ws.InitGit();
        _ws.WriteDoc("docs/storage.md", StorageDoc());
        _ws.Commit("one");
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        await _ws.InitBaselineAsync();
        _ws.Move("docs/storage.md", "docs/guides/storage.md");
        _ws.Commit("move");
        Assert.Equal("RELINK", await BucketAsync("TC-101"));
        StageIdentical("TC-101");
        var baselineBefore = _ws.ReadBaseline("storage");

        var batch = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        var result = Result(batch, "TC-101");
        Assert.Equal("unchanged_pending", result.GetProperty("outcome").GetString());
        Assert.Equal("RELINK", result.GetProperty("bucket").GetString());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.Equal("RELINK", await BucketAsync("TC-101"));
    }

    [Theory]
    [InlineData("LIGHT")]
    [InlineData("COSMETIC")]
    [InlineData("UNCHANGED")]
    public async Task AReviewableBucket_ReturnedUnchanged_IsRecorded_AndLeavesTriage(string bucket)
    {
        Assert.Equal(bucket, await ArrangeAsync(bucket));
        StageIdentical("TC-101");
        var fpBefore = _ws.RefFingerprint("storage", "TC-101", Sharing);

        var batch = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal("unchanged", Result(batch, "TC-101").GetProperty("outcome").GetString());
        Assert.Equal(0, batch.GetProperty("unchanged_pending").GetInt32());
        Assert.Null(await BucketAsync("TC-101"));
        if (bucket != "UNCHANGED")
        {
            Assert.NotEqual(fpBefore, _ws.RefFingerprint("storage", "TC-101", Sharing));
        }
    }

    [Fact]
    public async Task ATestWithoutABaselineEntry_IsRecorded()
    {
        _ws.WriteDoc("docs/storage.md", Doc());
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-102", [Quotas]); // after the baseline: no entry
        _ws.WriteDoc("docs/storage.md", Doc(quota: "45GB"));
        StageIdentical("TC-102");

        var batch = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal("unchanged", Result(batch, "TC-102").GetProperty("outcome").GetString());
        Assert.NotNull(_ws.RefFingerprint("storage", "TC-102", Quotas));
    }

    [Fact]
    public async Task KeepBaseline_AnIdenticalRepairOfALoadTest_IsUnchanged_AndRecordsNothing()
    {
        Assert.Equal("LOAD", await ArrangeAsync("LOAD"));
        _ws.StageRepair("storage", "TC-101", _ws.Load("storage", "TC-101"));
        var baselineBefore = _ws.ReadBaseline("storage");

        var batch = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all", "--keep-baseline");

        Assert.Equal("unchanged", Result(batch, "TC-101").GetProperty("outcome").GetString());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));
        Assert.Equal("LOAD", await BucketAsync("TC-101"));
    }

    [Fact]
    public async Task SingleForm_ALoadTestReturnedUnchanged_IsPending_Exit0()
    {
        Assert.Equal("LOAD", await ArrangeAsync("LOAD"));
        var from = _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));
        var baselineBefore = _ws.ReadBaseline("storage");

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from", from);

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal("unchanged_pending", result.GetProperty("outcome").GetString());
        Assert.True(result.GetProperty("unchanged").GetBoolean());
        Assert.Equal(0, result.GetProperty("persisted").GetInt32());
        Assert.Equal("LOAD", result.GetProperty("bucket").GetString());
        Assert.NotEmpty(result.GetProperty("why").EnumerateArray());
        Assert.Equal(baselineBefore, _ws.ReadBaseline("storage"));

        var (exit, human, _) = await _ws.RunAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from", from);

        Assert.Equal(0, exit);
        Assert.Contains($"Test 'TC-101' {Pending}.", human);
        Assert.Equal("LOAD", await BucketAsync("TC-101"));
    }

    [Fact]
    public async Task TheBatchSummary_NamesThePendingChange_OnStderrInJsonMode()
    {
        Assert.Equal("LOAD", await ArrangeAsync("LOAD"));
        StageIdentical("TC-101");

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.Equal(0, exit);
        Assert.Equal(JsonValueKind.Object, BaselineTestWorkspace.Json(output).ValueKind);
        Assert.Contains($"TC-101: {Pending}", error);
        Assert.Contains("unchanged_pending=1", error);
    }

    [Fact]
    public async Task RealInitWorkspace_LoadIsPending_LightIsRecorded()
    {
        using var ws = new BaselineTestWorkspace(writeConfig: false);
        await ws.InitWithRealTemplateAsync();
        await AssertLoadPendingLightRecordedAsync(ws, async () =>
        {
            var (exit, _, error) = await ws.RunAsync("ai", "ingest-tests", "storage", "--from",
                ws.GeneratedPayload(("TC-101", [Quotas], []), ("TC-102", [Sharing], [])), "--output-format", "json");
            Assert.True(exit == 0, error);
        });
    }

    [Fact]
    public async Task Pre089Workspace_LoadIsPending_LightIsRecorded()
    {
        // FR-025: 089 changes no on-disk format, so a workspace baselined with source-baseline --init is a pre-089 one.
        await AssertLoadPendingLightRecordedAsync(_ws, async () =>
        {
            _ws.WriteTest("storage", "TC-101", [Quotas]);
            _ws.WriteTest("storage", "TC-102", [Sharing]);
            await _ws.InitBaselineAsync();
        });
    }

    [Fact]
    public async Task APartlyBaselinedEntry_WithALightChange_IsRecorded_IncludingTheNewRef()
    {
        // Analysis U1: ref A (Sharing) baselined; ref B (Quotas) added to the test later, so it has no fingerprint.
        _ws.WriteDoc("docs/storage.md", Doc());
        _ws.WriteTest("storage", "TC-101", [Sharing]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-101", [Sharing, Quotas]);
        _ws.WriteDoc("docs/storage.md", Doc(sharing: SharingText.Replace("by link", "through a link")));
        var sharingBefore = _ws.RefFingerprint("storage", "TC-101", Sharing);
        Assert.Null(_ws.RefFingerprint("storage", "TC-101", Quotas));
        StageIdentical("TC-101");

        var batch = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal("unchanged", Result(batch, "TC-101").GetProperty("outcome").GetString());
        Assert.NotNull(_ws.RefFingerprint("storage", "TC-101", Quotas));
        Assert.NotEqual(sharingBefore, _ws.RefFingerprint("storage", "TC-101", Sharing));
    }

    private static async Task AssertLoadPendingLightRecordedAsync(BaselineTestWorkspace ws, Func<Task> createTests)
    {
        ws.WriteDoc("docs/storage.md", Doc());
        await createTests();
        ws.WriteDoc("docs/storage.md", Doc(quota: "45GB", sharing: SharingText.Replace("by link", "through a link")));
        ws.StageEdit("storage", "TC-101", ws.Load("storage", "TC-101"));
        ws.StageEdit("storage", "TC-102", ws.Load("storage", "TC-102"));
        var loadEntryBefore = Entry(ws, "TC-101");

        var batch = await ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal("unchanged_pending", Result(batch, "TC-101").GetProperty("outcome").GetString());
        Assert.Equal("LOAD", Result(batch, "TC-101").GetProperty("bucket").GetString());
        Assert.Equal("unchanged", Result(batch, "TC-102").GetProperty("outcome").GetString());
        Assert.Equal(loadEntryBefore, Entry(ws, "TC-101"));
        var triage = await ws.TriageAsync("storage", "--dry-run");
        Assert.Equal("LOAD", BaselineTestWorkspace.GroupOf(triage, "TC-101").GetProperty("bucket").GetString());
        Assert.Equal(JsonValueKind.Undefined, BaselineTestWorkspace.GroupOf(triage, "TC-102").ValueKind);
    }
}
