using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;
using Spectra.Core.Parsing;
using Spectra.Core.Update;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>Spec 084 User Story 1 (FR-012–FR-023): <c>spectra ai update-triage</c>.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateTriageCommandTests : IDisposable
{
    private const string StorageDoc =
        "# Storage\n\n## Quotas\n\nEach user gets 10GB of storage space. Users must log in first.\n\n"
        + "## Archive tier\n\nOld files move to the archive after 90 days.\n\n## Sharing\n\nFiles can be shared by link.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public UpdateTriageCommandTests()
    {
        _ws.WriteDoc("docs/storage.md", StorageDoc);
        _ws.WriteCriteria("docs/storage.criteria.yaml",
            ("AC-STORAGE-001", "Each user MUST get 10GB"), ("AC-STORAGE-002", "Old files MUST move after 90 days"));
    }

    public void Dispose() => _ws.Dispose();

    private void EditDoc(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    private static JsonElement Group(JsonElement triage, string id)
    {
        var group = BaselineTestWorkspace.GroupOf(triage, id);
        Assert.True(group.ValueKind == JsonValueKind.Object, $"{id} is in no group: {triage}");
        return group;
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    // ---- SC-001: the problem this spec fixes ----

    [Fact]
    public async Task NumberChange_IsUpToDateForTheLegacyClassifier_ButLoadForTriage()
    {
        const string test =
            "---\nid: TC-101\npriority: high\nsource_refs:\n  - \"docs/storage.md#Quotas\"\n---\n\n"
            + "# Storage Quotas\n\n## Steps\n\n1. Users storage space\n\n"
            + "## Expected Result\n\nEach user gets storage space\n";
        Directory.CreateDirectory(_ws.PathOf("test-cases/storage"));
        File.WriteAllText(_ws.PathOf("test-cases/storage/TC-101.md"), test);
        _ws.RebuildIndex("storage");
        await _ws.InitBaselineAsync();

        EditDoc("10GB", "50GB");

        var parsed = new TestCaseParser().Parse(test, "TC-101.md").Value!;
        var legacy = new TestClassifier().Classify(parsed, File.ReadAllText(_ws.PathOf("docs/storage.md")), []);
        Assert.Equal(UpdateClassification.UpToDate, legacy.Classification);

        var triage = await _ws.TriageAsync();
        var group = Group(triage, "TC-101");
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());
        Assert.Equal(["numbers: 10gb→50gb"], Strings(group.GetProperty("why")));
    }

    // ---- acceptance scenarios ----

    [Fact]
    public async Task ParaphrasedTest_OnAnUnchangedSection_IsUnchanged()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], title: "Completely different wording");
        await _ws.InitBaselineAsync();

        var triage = await _ws.TriageAsync();

        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "unchanged"));
        Assert.Empty(triage.GetProperty("groups").EnumerateArray());
    }

    [Fact]
    public async Task MarkupOnlyChange_IsCosmetic_AutoAcked_ForThatRefOnly()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"]);
        await _ws.InitBaselineAsync();
        var before = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        EditDoc("Each user gets", "**Each** user gets");

        var triage = await _ws.TriageAsync();

        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "cosmetic"));
        Assert.Equal(1, triage.GetProperty("auto_acked").GetInt32());
        Assert.Empty(triage.GetProperty("groups").EnumerateArray());
        var after = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.NotEqual(before.GetProperty("docs/storage.md#Quotas").GetString(), after.GetProperty("docs/storage.md#Quotas").GetString());
        Assert.Equal(before.GetProperty("docs/storage.md#Sharing").GetString(), after.GetProperty("docs/storage.md#Sharing").GetString());

        var next = await _ws.TriageAsync();
        Assert.Equal(1, BaselineTestWorkspace.Count(next, "storage", "unchanged"));
    }

    [Fact]
    public async Task NoAutoAck_ReportsCosmetic_AndLeavesTheBaseline()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        var before = _ws.ReadBaseline("storage");
        EditDoc("Each user gets", "**Each** user gets");

        var triage = await _ws.TriageAsync("storage", "--no-auto-ack");

        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "cosmetic"));
        Assert.Equal(0, triage.GetProperty("auto_acked").GetInt32());
        Assert.Equal(before, _ws.ReadBaseline("storage"));
    }

    [Fact]
    public async Task RewordedSentence_IsLight()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Sharing"]);
        await _ws.InitBaselineAsync();
        EditDoc("Files can be shared by link.", "Files can be shared with a link.");

        var group = Group(await _ws.TriageAsync(), "TC-101");

        Assert.Equal("LIGHT", group.GetProperty("bucket").GetString());
    }

    [Fact]
    public async Task ModalChange_IsLoad()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        EditDoc("Users must log in first.", "Users should log in first.");

        var group = Group(await _ws.TriageAsync(), "TC-101");

        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());
        Assert.Equal(["modal: must→should"], Strings(group.GetProperty("why")));
    }

    [Theory]
    [InlineData("## Quotas", "## Storage quotas", "anchor not found")]
    [InlineData(null, null, "not found; not in a git repository")] // Spec 086 FR-013: a missing file names the git reason
    public async Task RenamedHeadingOrDeletedFile_IsRemoved(string? from, string? to, string reason)
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        if (from is null) File.Delete(_ws.PathOf("docs/storage.md"));
        else EditDoc(from, to!);

        var triage = await _ws.TriageAsync();

        var removed = Assert.Single(triage.GetProperty("removed").EnumerateArray());
        Assert.Equal("TC-101", removed.GetProperty("test").GetString());
        Assert.Equal("docs/storage.md#Quotas", removed.GetProperty("ref").GetString());
        Assert.Equal(reason, removed.GetProperty("reason").GetString());
        Assert.Equal("REMOVED", Group(triage, "TC-101").GetProperty("bucket").GetString());
    }

    [Fact]
    public async Task ChangedAndDeletedCriteria()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Sharing"], ["AC-STORAGE-001"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Sharing"], ["AC-STORAGE-002"]);
        await _ws.InitBaselineAsync();
        _ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-001", "Each user MUST get 50GB"));

        var triage = await _ws.TriageAsync();

        Assert.Equal("CRITERIA_CHANGED", Group(triage, "TC-101").GetProperty("bucket").GetString());
        Assert.Equal("CRITERIA_REMOVED", Group(triage, "TC-102").GetProperty("bucket").GetString());
        Assert.Contains(triage.GetProperty("removed").EnumerateArray(),
            r => r.GetProperty("test").GetString() == "TC-102" && r.GetProperty("criterion").GetString() == "AC-STORAGE-002");
    }

    [Fact]
    public async Task SharedChangedRefs_AreGrouped_AndChunkedAtTheConfiguredSize()
    {
        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!;
        config["update"]!["group_size"] = 4;
        File.WriteAllText(_ws.PathOf("spectra.config.json"), config.ToJsonString());
        for (var i = 1; i <= 10; i++)
        {
            _ws.WriteTest("storage", $"TC-1{i:D2}", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"]);
        }

        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");
        EditDoc("by link", "by email");

        var groups = (await _ws.TriageAsync()).GetProperty("groups").EnumerateArray().ToList();

        Assert.Equal([4, 4, 2], groups.Select(g => g.GetProperty("tests").GetArrayLength()));
        Assert.All(groups, g => Assert.Equal(2, g.GetProperty("refs").GetArrayLength()));
        Assert.Equal(["G1", "G2", "G3"], groups.Select(g => g.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task NoTests_ListsUnreferencedHeadingsOfAChangedDoc()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");

        var noTests = (await _ws.TriageAsync()).GetProperty("no_tests").EnumerateArray()
            .Select(n => $"{n.GetProperty("doc").GetString()} › {n.GetProperty("heading").GetString()}").ToList();

        // "Storage" contains the referenced Quotas section, so it is covered; the two siblings are not.
        Assert.Equal(["docs/storage.md › Archive tier", "docs/storage.md › Sharing"], noTests);
    }

    [Fact]
    public async Task NoTests_IsEmpty_WhenAWholeFileRefCoversTheDoc()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md"]);
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");

        Assert.Empty((await _ws.TriageAsync()).GetProperty("no_tests").EnumerateArray());
    }

    [Fact]
    public async Task NoTests_ParentSectionRef_CoversItsChildren()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Storage"]);
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");

        Assert.Empty((await _ws.TriageAsync()).GetProperty("no_tests").EnumerateArray());
    }

    [Fact]
    public async Task PartialEntry_IsCountedAsPartial()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"]);

        var triage = await _ws.TriageAsync();

        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "partial"));
        Assert.Equal(2, BaselineTestWorkspace.Count(triage, "storage", "unchanged"));
        var unbaselined = Assert.Single(triage.GetProperty("unbaselined").EnumerateArray());
        Assert.Equal(1, unbaselined.GetProperty("partial").GetInt32());
    }

    [Fact]
    public async Task EmptySuite_IsReportedWithZeroCounts()
    {
        Directory.CreateDirectory(_ws.PathOf("test-cases/empty"));
        File.WriteAllText(_ws.PathOf("test-cases/empty/_index.json"), "{\"suite\":\"empty\",\"tests\":[]}");

        var triage = await _ws.TriageAsync("empty");

        var suite = Assert.Single(triage.GetProperty("suites").EnumerateArray());
        Assert.Equal(0, suite.GetProperty("tests").GetInt32());
        Assert.All(suite.GetProperty("counts").EnumerateObject(), c => Assert.Equal(0, c.Value.GetInt32()));
        Assert.Null(_ws.ReadBaseline("empty"));
        var detail = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf(".spectra/update-triage.json")));
        Assert.Empty(detail.GetProperty("tests").EnumerateArray());
    }

    [Fact]
    public async Task AutoAck_KeepsAStaleKey()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]); // Sharing dropped by hand → stale key
        EditDoc("Each user gets", "**Each** user gets");

        await _ws.TriageAsync();

        Assert.True(_ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs")
            .TryGetProperty("docs/storage.md#Sharing", out _));
    }

    [Fact]
    public async Task DetailFile_HasDiffsAndCurrentFingerprints()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");

        var triage = await _ws.TriageAsync();

        Assert.Equal(".spectra/update-triage.json", triage.GetProperty("detail_file").GetString());
        var detail = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf(".spectra/update-triage.json")));
        var test = Assert.Single(detail.GetProperty("tests").EnumerateArray());
        var r = Assert.Single(test.GetProperty("refs").EnumerateArray());
        Assert.Equal("LOAD", r.GetProperty("state").GetString());
        Assert.Equal(["10gb"], Strings(r.GetProperty("diff")[0].GetProperty("removed")));
        Assert.Equal(["50gb"], Strings(r.GetProperty("diff")[1].GetProperty("added")));
        Assert.Equal(r.GetProperty("current").GetString(),
            test.GetProperty("current").GetProperty("refs").GetProperty("docs/storage.md#Quotas").GetString());
        Assert.Equal("G1", detail.GetProperty("groups")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task DryRun_WritesNothing_AndReportsWouldAutoAck()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        EditDoc("Each user gets", "**Each** user gets");
        var before = _ws.Snapshot();

        var triage = await _ws.TriageAsync("storage", "--dry-run");

        Assert.Equal(before, _ws.Snapshot());
        Assert.Equal(1, triage.GetProperty("would_auto_ack").GetInt32());
        Assert.Equal(JsonValueKind.Null, triage.GetProperty("detail_file").ValueKind);
    }

    [Fact]
    public async Task Notes_ReportAmbiguousAnchors_AndDuplicateCriteria()
    {
        _ws.WriteDoc("docs/dup.md", "# Dup\n\n## Limits\n\nfirst\n\n## Limits\n\nsecond\n");
        _ws.WriteCriteria("docs/other.criteria.yaml", ("AC-STORAGE-001", "a duplicate definition"));
        _ws.WriteTest("storage", "TC-101", ["docs/dup.md#Limits"], ["AC-STORAGE-001"]);
        await _ws.InitBaselineAsync();

        var notes = Strings((await _ws.TriageAsync()).GetProperty("notes"));

        Assert.Contains(notes, n => n.StartsWith("docs/dup.md#Limits: more than one heading", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("criterion AC-STORAGE-001 is defined more than once", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Output_JsonOnStdout_SummaryOnStderr_AndExit0WithChanges()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");

        var (exit, output, error) = await _ws.RunAsync("ai", "update-triage", "--suite", "storage", "--output-format", "json");

        Assert.Equal(0, exit);
        Assert.Single(output.Trim().Split('\n'));
        var json = BaselineTestWorkspace.Json(output);
        Assert.Equal("update-triage", json.GetProperty("command").GetString());
        Assert.Contains("Update triage — storage (1 tests)", error);
        Assert.Contains("G1", error);

        var human = await _ws.RunAsync("ai", "update-triage", "--suite", "storage");
        Assert.Contains("Update triage — storage (1 tests)", human.Out);
        Assert.Contains("numbers: 10gb→50gb", human.Out);
    }
}
