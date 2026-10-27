using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Diagnostics;

namespace Spectra.CLI.Tests.Generate;

/// <summary>
/// Spec 099 F4 (FR-013/FR-014, SC-005): <c>ingest-update --all</c> parses each staged file's JSON once; before 099 the
/// command, the update ingestor and validation each parsed it (3 per file). Outcomes, codes and the exit code are
/// unchanged (the messages are pinned by UpdatedTestIngestorTests, MultipleTestsTests and the staging tests).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class StagedJsonParseCountTests : IDisposable
{
    private const string Suite = "storage";
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public StagedJsonParseCountTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB**.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103", "TC-104", "TC-105", "TC-106", "TC-107", "TC-108" })
        {
            _ws.WriteTest(Suite, id, [Quotas]);
        }

        _ws.InitBaselineAsync(Suite).GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private void StageValid(string id, string expected) =>
        _ws.StageEdit(Suite, id, _ws.Load(Suite, id) with { ExpectedResult = expected });

    [Fact]
    public async Task All_ParsesEachStagedFileOnce()
    {
        StageValid("TC-101", "Edited one");
        StageValid("TC-102", "Edited two");
        StageValid("TC-103", "Edited three");

        using var scope = ParseCounter.Begin();
        var (exit, output, error) = await _ws.IngestAllAsync(Suite);

        Assert.True(exit == 0, output + error);
        Assert.Equal(3, scope.JsonParses);
    }

    [Fact]
    public async Task TestId_ParsesTheEditOnce()
    {
        var path = _ws.PathOf(".spectra/edit.json");
        File.WriteAllText(path, "[\n" + Spectra.CLI.Generation.UpdatePromptCompiler.SerializeTest(
            _ws.Load(Suite, "TC-101") with { ExpectedResult = "Edited" }) + "\n]\n");

        using var scope = ParseCounter.Begin();
        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-update", Suite, "--test-id", "TC-101", "--from", path, "--output-format", "json");

        Assert.True(exit == 0, output + error);
        Assert.Equal(1, scope.JsonParses);
    }

    [Fact]
    public async Task All_FailureCodes_AreUnchanged()
    {
        StageValid("TC-101", "Edited");
        _ws.StageRaw(Suite, "TC-102", "   \n");
        _ws.StageRaw(Suite, "TC-103", "Here is the edit, but no array.");
        _ws.StageRaw(Suite, "TC-104", "[\n  {\"id\": \"TC-104\", \"title\": \"Cut off\"");
        _ws.StageRaw(Suite, "TC-105", "[ {\"id\": \"TC-105\", \"title\": } ]");
        _ws.StageRaw(Suite, "TC-106", "[{\"id\":\"TC-106\",\"title\":\"A\",\"priority\":\"high\",\"steps\":[\"x\"],\"expected_result\":\"y\"},{\"id\":\"TC-199\",\"title\":\"B\"}]");
        _ws.StageRaw(Suite, "TC-107", "[{\"id\":\"TC-999\",\"title\":\"A\",\"priority\":\"high\",\"steps\":[\"x\"],\"expected_result\":\"y\"}]");
        _ws.StageEdit(Suite, "TC-108", _ws.Load(Suite, "TC-108") with { Priority = Spectra.Core.Models.Priority.Low });

        var (exit, output, _) = await _ws.IngestAllAsync(Suite);

        Assert.Equal(8, exit);
        var results = VerdictWorkspace.Json(output).GetProperty("results").EnumerateArray()
            .ToDictionary(r => r.GetProperty("test").GetString()!,
                r => r.GetProperty("outcome").GetString() + (r.TryGetProperty("error_code", out var c) ? ":" + c.GetString() : ""));
        Assert.Equal("written", results["TC-101"]);
        Assert.Equal("failed:EMPTY_CONTENT", results["TC-102"]);
        Assert.Equal("failed:EMPTY_CONTENT", results["TC-103"]);
        Assert.Equal("failed:TRUNCATED", results["TC-104"]);
        Assert.Equal("failed:MALFORMED_JSON", results["TC-105"]);
        Assert.Equal("failed:MULTIPLE_TESTS", results["TC-106"]);
        Assert.Equal("failed:ID_MISMATCH", results["TC-107"]);
        Assert.Equal("failed:DRIFT_DETECTED", results["TC-108"]);
    }
}
