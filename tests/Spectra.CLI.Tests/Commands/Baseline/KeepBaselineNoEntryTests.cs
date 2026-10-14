using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 098 F5 (FR-012, SC-005): a keep-baseline ingest (a critic repair) never creates a baseline entry. A
/// never-baselined test stays unbaselined, so triage keeps listing it and the user decides when its baseline starts; an
/// existing entry only gains missing keys. On 986da01 the repair created a full entry at the current docs, hiding every
/// doc change since the test was written.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class KeepBaselineNoEntryTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public KeepBaselineNoEntryTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Sharing\n\nShared by link.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
    }

    public void Dispose() => _ws.Dispose();

    private string Payload(string id, string[] refs)
    {
        var path = _ws.PathOf($".spectra/edit-{id}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new[]
        {
            new { id, title = $"Repaired {id}", priority = "high", steps = new[] { "Do the thing" }, expected_result = "It works", source_refs = refs, criteria = Array.Empty<string>() }
        }));
        return path;
    }

    private bool HasEntry(string id) =>
        _ws.ReadBaseline("storage") is { } text
        && BaselineTestWorkspace.Json(text).GetProperty("tests").TryGetProperty(id, out _);

    [Fact]
    public async Task ANeverBaselinedTest_Repaired_StaysUnbaselined_Single()
    {
        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            Payload("TC-101", ["docs/storage.md#Quotas"]), "--keep-baseline");

        Assert.False(HasEntry("TC-101"));
        Assert.Equal(1, result.GetProperty("baseline").GetProperty("left_unbaselined").GetInt32());
        Assert.Equal(0, result.GetProperty("baseline").GetProperty("created").GetInt32());
        Assert.Equal(1, BaselineTestWorkspace.Count(await _ws.TriageAsync(), "storage", "unbaselined"));
    }

    [Fact]
    public async Task ANeverBaselinedTest_Repaired_StaysUnbaselined_Batch()
    {
        Directory.CreateDirectory(_ws.PathOf(".spectra/repairs/storage"));
        File.Copy(Payload("TC-101", ["docs/storage.md#Quotas"]), _ws.PathOf(".spectra/repairs/storage/repaired-TC-101.json"));

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--all", "--keep-baseline");

        Assert.False(HasEntry("TC-101"));
        Assert.Equal(1, result.GetProperty("baseline").GetProperty("left_unbaselined").GetInt32());
    }

    [Fact]
    public async Task ABaselinedTest_Repaired_KeepsItsFingerprints_AndGainsMissingKeys()
    {
        await _ws.InitBaselineAsync();
        var quotas = BaselineTestWorkspace.Json(_ws.ReadBaseline("storage")!).GetProperty("tests").GetProperty("TC-101")
            .GetProperty("refs").GetProperty("docs/storage.md#Quotas").GetString();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 50GB.\n\n## Sharing\n\nShared by link.\n");

        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            Payload("TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"]), "--keep-baseline");

        var refs = BaselineTestWorkspace.Json(_ws.ReadBaseline("storage")!).GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.Equal(quotas, refs.GetProperty("docs/storage.md#Quotas").GetString());
        Assert.True(refs.TryGetProperty("docs/storage.md#Sharing", out _));
        Assert.False(result.GetProperty("baseline").TryGetProperty("left_unbaselined", out _));
    }

    [Fact]
    public async Task ANormalIngest_StillCreatesTheEntry()
    {
        var result = await _ws.RunJsonAsync("ai", "ingest-update", "storage", "--test-id", "TC-101", "--from",
            Payload("TC-101", ["docs/storage.md#Quotas"]));

        Assert.True(HasEntry("TC-101"));
        Assert.Equal(1, result.GetProperty("baseline").GetProperty("created").GetInt32());
    }
}
