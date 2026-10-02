using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>Spec 084 T5 (FR-027): <c>ingest-tests</c> and <c>ingest-update</c> record baseline entries.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestBaselineRecordingTests : IDisposable
{
    private const string StorageDoc = "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Archive tier\n\nOld files move after 90 days.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public IngestBaselineRecordingTests()
    {
        _ws.WriteDoc("docs/storage.md", StorageDoc);
        _ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-001", "Each user MUST get 10GB"));
    }

    public void Dispose() => _ws.Dispose();

    private JsonElement Entry(string id) => _ws.BaselineJson("storage").GetProperty("tests").GetProperty(id);

    private static string[] Keys(JsonElement map) => map.EnumerateObject().Select(p => p.Name).ToArray();

    private string EditedPayload(string id, string[] refs, string[] criteria)
    {
        var path = _ws.PathOf($".spectra/updated-{id}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new[]
        {
            new
            {
                id, title = $"Test {id}", priority = "high", steps = new[] { "Do the thing" },
                expected_result = "It works", source_refs = refs, criteria
            }
        }));
        return path;
    }

    [Fact]
    public async Task IngestTests_RecordsEntries_ForEveryPersistedTest()
    {
        var payload = _ws.GeneratedPayload(
            ("TC-101", ["docs/storage.md#Quotas"], ["AC-STORAGE-001"]),
            ("TC-102", ["docs/storage.md#Archive-tier"], []));

        var result = await _ws.RunJsonAsync("ai", "ingest-tests", "storage", "--from", payload);

        Assert.Equal(2, result.GetProperty("baseline").GetProperty("created").GetInt32());
        Assert.Equal(["docs/storage.md#Quotas"], Keys(Entry("TC-101").GetProperty("refs")));
        Assert.Equal(["AC-STORAGE-001"], Keys(Entry("TC-101").GetProperty("criteria")));
        Assert.Equal(["docs/storage.md#Archive-tier"], Keys(Entry("TC-102").GetProperty("refs")));
        Assert.Equal(2, _ws.SnapshotFiles().Length);
    }

    [Fact]
    public async Task IngestUpdate_ReplacesTheEditedTestsEntry_Only()
    {
        await _ws.RunJsonAsync("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(
            ("TC-101", ["docs/storage.md#Quotas"], ["AC-STORAGE-001"]),
            ("TC-102", ["docs/storage.md#Quotas"], [])));
        var other = Entry("TC-102").GetRawText();

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101",
            "--from", EditedPayload("TC-101", ["docs/storage.md#Archive-tier"], []));

        Assert.Equal(1, result.GetProperty("baseline").GetProperty("updated").GetInt32());
        Assert.Equal(["docs/storage.md#Archive-tier"], Keys(Entry("TC-101").GetProperty("refs")));
        Assert.Empty(Keys(Entry("TC-101").GetProperty("criteria")));
        Assert.Equal(other, Entry("TC-102").GetRawText());
    }

    [Fact]
    public async Task IngestUpdateAll_RecordsEachWrittenTest()
    {
        await _ws.RunJsonAsync("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(
            ("TC-101", ["docs/storage.md#Quotas"], []), ("TC-102", ["docs/storage.md#Quotas"], [])));
        Directory.CreateDirectory(_ws.PathOf(".spectra/updates/storage"));
        foreach (var id in new[] { "TC-101", "TC-102" })
        {
            File.Copy(EditedPayload(id, ["docs/storage.md#Archive-tier"], []), _ws.PathOf($".spectra/updates/storage/updated-{id}.json"));
        }

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all");

        Assert.Equal(2, result.GetProperty("written").GetInt32());
        Assert.Equal(2, result.GetProperty("baseline").GetProperty("updated").GetInt32());
        Assert.Equal(["docs/storage.md#Archive-tier"], Keys(Entry("TC-101").GetProperty("refs")));
        Assert.Equal(["docs/storage.md#Archive-tier"], Keys(Entry("TC-102").GetProperty("refs")));
    }

    [Fact]
    public async Task FailedIngest_WritesNoBaseline_AndKeepsItsFailureShape()
    {
        var payload = _ws.PathOf(".spectra/bad.json");
        File.WriteAllText(payload, "[ {\"id\":\"TC-101\",\"title\":\"A\",");

        var (exit, _, error) = await _ws.RunAsync("ai", "ingest-tests", "storage", "--from", payload, "--output-format", "json");

        Assert.NotEqual(0, exit);
        Assert.Null(_ws.ReadBaseline("storage"));
        // The pre-084 content/schema failure shape is unchanged (FR-027).
        var failure = BaselineTestWorkspace.Json(error.Trim().Split('\n').Last());
        Assert.Equal(["success", "error_code", "errors"], failure.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task UnreadableBaseline_PersistsNothing()
    {
        Directory.CreateDirectory(_ws.PathOf("test-cases/storage"));
        File.WriteAllText(_ws.BaselinePath("storage"), "{ not json");
        var before = _ws.Snapshot();

        var (exit, _, error) = await _ws.RunAsync("ai", "ingest-tests", "storage", "--from",
            _ws.GeneratedPayload(("TC-101", ["docs/storage.md#Quotas"], [])), "--output-format", "json");

        Assert.Equal(1, exit);
        var failure = BaselineTestWorkspace.Json(error.Trim());
        Assert.Equal("BASELINE_UNREADABLE", failure.GetProperty("error_code").GetString());
        Assert.Equal("ingest-tests", failure.GetProperty("command").GetString());
        Assert.Contains("test-cases/storage/_source-baseline.json", failure.GetProperty("errors")[0].GetString());
        Assert.False(File.Exists(_ws.PathOf("test-cases/storage/TC-101.md")));
        Assert.Equal(before.Where(kv => kv.Key.StartsWith("test-cases/")), _ws.Snapshot().Where(kv => kv.Key.StartsWith("test-cases/")));
    }

    [Fact]
    public async Task DeletedTestsEntry_IsPrunedOnTheNextIngest()
    {
        await _ws.RunJsonAsync("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(
            ("TC-101", ["docs/storage.md#Quotas"], []), ("TC-102", ["docs/storage.md#Quotas"], [])));
        File.Delete(_ws.PathOf("test-cases/storage/TC-102.md"));

        await _ws.RunJsonAsync("ai", "ingest-tests", "storage", "--from", _ws.GeneratedPayload(("TC-103", ["docs/storage.md#Quotas"], [])));

        Assert.Equal(["TC-101", "TC-103"], Keys(_ws.BaselineJson("storage").GetProperty("tests")));
    }
}
