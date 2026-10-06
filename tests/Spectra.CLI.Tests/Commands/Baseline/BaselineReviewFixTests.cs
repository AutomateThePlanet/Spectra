using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 085 (review fixes for 084), command level: F1 resolution and cleaned <c>no_tests</c> (T3), compatibility with
/// 084 (T9, FR-022), pruning only when the whole suite parsed (T4), dead refs kept on rebuild (T5), and repairs that
/// keep the baseline (T7).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BaselineReviewFixTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private JsonElement Entry(string id, string suite = "storage") =>
        _ws.BaselineJson(suite).GetProperty("tests").GetProperty(id);

    private static string[] Keys(JsonElement map) => map.EnumerateObject().Select(p => p.Name).ToArray();

    private string EditedPayload(string id, string[] refs, string[] criteria, string title = "Edited")
    {
        var path = _ws.PathOf($".spectra/edit-{id}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new[]
        {
            new { id, title = $"{title} {id}", priority = "high", steps = new[] { "Do the thing" }, expected_result = "It works", source_refs = refs, criteria }
        }));
        return path;
    }

    private void Stage(string suite, string id, string payload)
    {
        Directory.CreateDirectory(_ws.PathOf($".spectra/updates/{suite}"));
        File.Copy(payload, _ws.PathOf($".spectra/updates/{suite}/updated-{id}.json"), overwrite: true);
    }

    private static bool InRemoved(JsonElement triage, string test, string? @ref = null, string? criterion = null) =>
        triage.GetProperty("removed").EnumerateArray().Any(r =>
            r.GetProperty("test").GetString() == test
            && (@ref is null || (r.TryGetProperty("ref", out var x) && x.GetString() == @ref))
            && (criterion is null || (r.TryGetProperty("criterion", out var c) && c.GetString() == criterion)));

    // ---- T3 / F1 at command level ----

    [Fact]
    public async Task NoTests_ShowsCleanedHeadingText()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Archive <a id=\"archive\"></a>\n\nOld files.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 50GB.\n\n## Archive <a id=\"archive\"></a>\n\nOld files.\n");

        var noTests = (await _ws.TriageAsync()).GetProperty("no_tests").EnumerateArray().Select(n => n.GetProperty("heading").GetString());

        Assert.Equal(["Archive"], noTests);
    }

    [Fact]
    public async Task CustomIdRef_IsBaselinedAndUnchanged()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Title {#custom-id}\n\nText.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#custom-id"]);

        await _ws.InitBaselineAsync();

        Assert.Equal(["docs/storage.md#custom-id"], Keys(Entry("TC-101").GetProperty("refs")));
        var triage = await _ws.TriageAsync();
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "unchanged"));
        Assert.Equal(0, BaselineTestWorkspace.Count(triage, "storage", "partial"));
    }

    // ---- T9 / FR-021, FR-022: compatibility with 084 ----

    /// <summary>
    /// The compatibility fixture, byte-for-byte the files `specs/085-review-fixes-084` capture084.py wrote when the 084
    /// expected outputs below were captured.
    /// </summary>
    private static readonly Dictionary<string, string> Fixture = new()
    {
        ["spectra.config.json"] = "{\"source\":{\"local_dir\":\"docs/\"},\"tests\":{\"dir\":\"test-cases/\"},\"ai\":{}}",
        ["docs/guide.md"] = "# Guide\n\nIntro text.\n\n## Plain heading\n\nEach user gets 10GB.\n\n## **Bold** heading\n\nUsers must log in.\n\n## Use `code` here\n\nRun the tool.\n",
        ["docs/criteria/docs/guide.criteria.yaml"] = "criteria:\n- id: AC-GUIDE-001\n  text: Each user MUST get 10GB\n",
        ["test-cases/guide/TC-101.md"] = "---\nid: TC-101\npriority: high\nsource_refs:\n  - \"docs/guide.md#Plain-heading\"\ncriteria:\n  - AC-GUIDE-001\n---\n\n# Quota TC-101\n\n## Steps\n\n1. Do the thing\n\n## Expected Result\n\nIt works\n",
        ["test-cases/guide/TC-102.md"] = "---\nid: TC-102\npriority: high\nsource_refs:\n  - \"docs/guide.md#bold-heading\"\n---\n\n# Login TC-102\n\n## Steps\n\n1. Do the thing\n\n## Expected Result\n\nIt works\n",
        ["test-cases/guide/TC-103.md"] = "---\nid: TC-103\npriority: high\nsource_refs:\n  - \"docs/guide.md#Use-code-here\"\n---\n\n# Tool TC-103\n\n## Steps\n\n1. Do the thing\n\n## Expected Result\n\nIt works\n",
        ["test-cases/guide/TC-104.md"] = "---\nid: TC-104\npriority: high\nsource_refs:\n  - \"docs/guide.md\"\n---\n\n# Whole TC-104\n\n## Steps\n\n1. Do the thing\n\n## Expected Result\n\nIt works\n",
    };

    // Captured 2026-10-05T11:51:44Z by running capture084.py against the 2a43298 build (see tasks.md Notes). Do not edit.
    private const string Expected084Unchanged =
        "{\"command\":\"update-triage\",\"success\":true,\"scope\":{\"all_suites\":true},\"dry_run\":false,\"suites\":[{\"suite\":\"guide\",\"tests\":4,\"counts\":{\"unchanged\":4}}],\"groups\":[],\"removed\":[],\"unbaselined\":[],\"no_tests\":[],\"notes\":[],\"auto_acked\":0,\"detail_file\":\".spectra/update-triage.json\",\"warnings\":[]}";

    private const string Expected084Changed =
        "{\"command\":\"update-triage\",\"success\":true,\"scope\":{\"all_suites\":true},\"dry_run\":false,\"suites\":[{\"suite\":\"guide\",\"tests\":4,\"counts\":{\"load\":2,\"unchanged\":2}}],\"groups\":[{\"id\":\"G1\",\"suite\":\"guide\",\"bucket\":\"LOAD\",\"refs\":[{\"ref\":\"docs/guide.md#Plain-heading\",\"state\":\"LOAD\"}],\"tests\":[\"TC-101\"],\"why\":[\"numbers: 10gb→50gb\"]},{\"id\":\"G2\",\"suite\":\"guide\",\"bucket\":\"LOAD\",\"refs\":[{\"ref\":\"docs/guide.md\",\"state\":\"LOAD\"}],\"tests\":[\"TC-104\"],\"why\":[\"numbers: 10gb→50gb\"]}],\"removed\":[],\"unbaselined\":[],\"no_tests\":[],\"notes\":[],\"auto_acked\":0,\"detail_file\":\".spectra/update-triage.json\",\"warnings\":[]}";

    private void WriteFixture()
    {
        foreach (var (relative, text) in Fixture)
        {
            var path = _ws.PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
    }

    [Fact]
    public async Task UnchangedWorkspace_TriagesExactlyAs084_AndTheBaselineReSavesByteIdentically()
    {
        WriteFixture();
        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        var baseline = File.ReadAllBytes(_ws.BaselinePath("guide"));

        var unchanged = await _ws.RunAsync("ai", "update-triage", "--all-suites", "--no-auto-ack", "--output-format", "json");
        Assert.Equal(Expected084Unchanged, unchanged.Out.Trim());

        await _ws.RunJsonAsync("ai", "update-ack", "--suite", "guide", "--ids", "TC-101");
        Assert.Equal(baseline, File.ReadAllBytes(_ws.BaselinePath("guide")));

        _ws.WriteDoc("docs/guide.md", Fixture["docs/guide.md"].Replace("10GB", "50GB"));
        var changed = await _ws.RunAsync("ai", "update-triage", "--all-suites", "--no-auto-ack", "--output-format", "json");
        Assert.Equal(Expected084Changed, changed.Out.Trim());
    }

    [Fact]
    public async Task NeverResolvedRefs_PartialUnder084_AreAddedByInit()
    {
        _ws.WriteDoc("docs/a.md", "# A\n\n## Create customer records <a href=\"#create-customer-records\" id=\"create-customer-records\"></a>\n\nOne.\n");
        _ws.WriteDoc("docs/b.md", "# B\n\n## [Payments](payments.md)\n\nTwo.\n");
        _ws.WriteDoc("docs/c.md", "# C\n\n## Title {#custom-id}\n\nThree.\n");
        _ws.WriteTest("storage", "TC-201", ["docs/a.md#create-customer-records"]);
        _ws.WriteTest("storage", "TC-202", ["docs/b.md#payments"]);
        _ws.WriteTest("storage", "TC-203", ["docs/c.md#custom-id"]);
        // What 084's init wrote for these: entries with the never-resolving refs left out.
        File.WriteAllText(_ws.BaselinePath("storage"),
            "{\n  \"version\": 1,\n  \"tests\": {\n    \"TC-201\": {\n      \"refs\": {},\n      \"criteria\": {}\n    },\n"
            + "    \"TC-202\": {\n      \"refs\": {},\n      \"criteria\": {}\n    },\n    \"TC-203\": {\n      \"refs\": {},\n      \"criteria\": {}\n    }\n  }\n}\n");

        var before = await _ws.TriageAsync();
        Assert.Equal(3, BaselineTestWorkspace.Count(before, "storage", "partial"));

        await _ws.InitBaselineAsync();

        var after = await _ws.TriageAsync();
        Assert.Equal(0, BaselineTestWorkspace.Count(after, "storage", "partial"));
        Assert.Equal(3, BaselineTestWorkspace.Count(after, "storage", "unchanged"));
    }

    [Theory]
    [InlineData("## Quotas <a id=\"quotas\"></a>")]
    [InlineData("## [Quotas](quotas.md)")]
    [InlineData("## Quotas {#quotas}")]
    public async Task PlainHeadingThatGainsMarkup_IsCosmetic_NotRemoved(string marked)
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", $"# Storage\n\n{marked}\n\nEach user gets 10GB.\n");

        var triage = await _ws.TriageAsync();

        Assert.Empty(triage.GetProperty("removed").EnumerateArray());
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "cosmetic"));
        Assert.Equal(1, triage.GetProperty("auto_acked").GetInt32());
        Assert.Equal(1, BaselineTestWorkspace.Count(await _ws.TriageAsync(), "storage", "unchanged"));
    }

    // ---- T4 / F2: prune only when every test file parsed ----

    private const string Unparseable = "---\nid: TC-102\npriority: [\n---\n\n# Broken\n";
    private const string PruneWarning = "test-cases/storage/TC-102.md could not be parsed; no baseline entries were pruned in suite 'storage'.";

    private async Task SeedPruneSuiteAsync()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        File.WriteAllText(_ws.PathOf("test-cases/storage/TC-102.md"), Unparseable);
    }

    private void AssertKeptAndWarned(string writer, string output)
    {
        Assert.True(_ws.BaselineJson("storage").GetProperty("tests").TryGetProperty("TC-102", out _), $"{writer}: TC-102's entry was pruned");
        Assert.True(output.Contains(PruneWarning, StringComparison.Ordinal), $"{writer}: no prune warning in {output}");
    }

    [Fact]
    public async Task UnparseableTestFile_KeepsItsEntry_UnderEveryWriter()
    {
        await SeedPruneSuiteAsync();

        var ingest = await _ws.RunAsync("ai", "ingest-tests", "storage", "--from",
            _ws.GeneratedPayload(("TC-103", ["docs/storage.md#Quotas"], [])), "--output-format", "json");
        Assert.Equal(0, ingest.Exit);
        AssertKeptAndWarned("ingest-tests", ingest.Out + ingest.Err);

        var update = await _ws.RunAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            EditedPayload("TC-101", ["docs/storage.md#Quotas"], []), "--output-format", "json");
        Assert.Equal(0, update.Exit);
        AssertKeptAndWarned("ingest-update", update.Out + update.Err);

        Stage("storage", "TC-101", EditedPayload("TC-101", ["docs/storage.md#Quotas"], [], "Again"));
        var all = await _ws.RunAsync("ai", "ingest-update", "storage", "--all", "--output-format", "json");
        Assert.Equal(0, all.Exit);
        AssertKeptAndWarned("ingest-update --all", all.Out + all.Err);

        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\n**Each** user gets 10GB.\n");
        var triage = await _ws.RunAsync("ai", "update-triage", "--suite", "storage", "--output-format", "json");
        Assert.Equal(0, triage.Exit);
        Assert.Equal(2, BaselineTestWorkspace.Json(triage.Out).GetProperty("auto_acked").GetInt32()); // TC-101 and TC-103
        AssertKeptAndWarned("update-triage", triage.Out + triage.Err);

        var ack = await _ws.RunAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101", "--output-format", "json");
        Assert.Equal(0, ack.Exit);
        AssertKeptAndWarned("update-ack", ack.Out + ack.Err);

        var init = await _ws.RunAsync("ai", "source-baseline", "--init", "--suite", "storage", "--output-format", "json");
        Assert.Equal(0, init.Exit);
        AssertKeptAndWarned("source-baseline --init", init.Out + init.Err);
    }

    [Theory]
    [InlineData("update-ack")]
    [InlineData("source-baseline")]
    public async Task PruneWarning_IsAWarningLineOnStderr_InHumanMode(string command)
    {
        await SeedPruneSuiteAsync();
        string[] args = command == "update-ack"
            ? ["ai", "update-ack", "--suite", "storage", "--ids", "TC-101"]
            : ["ai", "source-baseline", "--init", "--suite", "storage"];

        var (exit, _, error) = await _ws.RunAsync(args);

        Assert.Equal(0, exit);
        Assert.Contains("Warning: " + PruneWarning, error);
    }

    [Fact]
    public async Task WhenEveryFileParses_ADeletedTestsEntryIsPruned()
    {
        await SeedPruneSuiteAsync();
        await _ws.RunJsonAsync("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(("TC-103", ["docs/storage.md#Quotas"], [])));
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]); // fixed
        File.Delete(_ws.PathOf("test-cases/storage/TC-103.md"));

        var ack = await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101");

        Assert.Equal(["TC-101", "TC-102"], Keys(_ws.BaselineJson("storage").GetProperty("tests")));
        Assert.DoesNotContain(ack.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("pruned"));
    }

    // ---- T5 / F3: rebuilds keep dead refs ----

    private async Task SeedDeadRefAsync()
    {
        _ws.WriteDoc("docs/a.md", "# A\n\n## Gone\n\nGone text.\n\n## Kept\n\nKept text.\n");
        _ws.WriteCriteria("docs/a.criteria.yaml", ("AC-X-001", "Gone MUST work"), ("AC-X-002", "Kept MUST work"));
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Gone", "docs/a.md#Kept"], ["AC-X-001"]);
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/a.md", "# A\n\n## Kept\n\nKept text.\n");
        _ws.WriteCriteria("docs/a.criteria.yaml", ("AC-X-002", "Kept MUST work"));
    }

    [Fact]
    public async Task IngestUpdateRebuild_KeepsDeadRefsReported()
    {
        await SeedDeadRefAsync();

        await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            EditedPayload("TC-101", ["docs/a.md#Gone", "docs/a.md#Kept"], ["AC-X-001"]));

        var triage = await _ws.TriageAsync();
        Assert.True(InRemoved(triage, "TC-101", @ref: "docs/a.md#Gone"));
        Assert.True(InRemoved(triage, "TC-101", criterion: "AC-X-001"));
    }

    [Fact]
    public async Task ForcedInit_KeepsDeadRefsReported()
    {
        await SeedDeadRefAsync();

        await _ws.InitBaselineAsync("storage", "--force");

        var triage = await _ws.TriageAsync();
        Assert.True(InRemoved(triage, "TC-101", @ref: "docs/a.md#Gone"));
        Assert.True(InRemoved(triage, "TC-101", criterion: "AC-X-001"));
    }

    [Fact]
    public async Task DroppingTheDeadRef_RemovesItsKey_AndTheTestIsNoLongerRemoved()
    {
        await SeedDeadRefAsync();

        await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            EditedPayload("TC-101", ["docs/a.md#Kept"], []));

        Assert.Equal(["docs/a.md#Kept"], Keys(Entry("TC-101").GetProperty("refs")));
        Assert.Empty(Keys(Entry("TC-101").GetProperty("criteria")));
        Assert.False(InRemoved(await _ws.TriageAsync(), "TC-101"));
    }

    // ---- T7 / F5: repairs keep the baseline ----

    private async Task SeedPendingLoadAsync()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Sharing\n\nShared by link.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 50GB.\n\n## Sharing\n\nShared by link.\n");
    }

    private async Task AssertStillLoad_AndSharingAdded()
    {
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-101");
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());
        Assert.Equal("docs/storage.md#Quotas", group.GetProperty("refs")[0].GetProperty("ref").GetString());
        Assert.Contains("docs/storage.md#Sharing", Keys(Entry("TC-101").GetProperty("refs")));
    }

    [Fact]
    public async Task RepairBatch_WithKeepBaseline_LeavesThePendingChange()
    {
        await SeedPendingLoadAsync();
        Stage("storage", "TC-101", EditedPayload("TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"], []));

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all", "--keep-baseline");

        Assert.Equal("keep", result.GetProperty("baseline").GetProperty("mode").GetString());
        await AssertStillLoad_AndSharingAdded();
    }

    [Fact]
    public async Task SingleRepair_WithKeepBaseline_LeavesThePendingChange()
    {
        await SeedPendingLoadAsync();

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            EditedPayload("TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"], []), "--keep-baseline");

        Assert.Equal("keep", result.GetProperty("baseline").GetProperty("mode").GetString());
        await AssertStillLoad_AndSharingAdded();
    }

    [Fact]
    public async Task UpdateWithoutKeepBaseline_AcceptsTheDocChange()
    {
        await SeedPendingLoadAsync();

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            EditedPayload("TC-101", ["docs/storage.md#Quotas"], []));

        Assert.Equal("rebuild", result.GetProperty("baseline").GetProperty("mode").GetString());
        Assert.Equal(1, BaselineTestWorkspace.Count(await _ws.TriageAsync(), "storage", "unchanged"));
    }
}
