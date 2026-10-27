using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 089 T2 (F2, FR-006–FR-008): an edit must hold exactly one test object. More than one fails with
/// <c>MULTIPLE_TESTS</c>, naming the count and the ids, and nothing is written — in the single form (exit 5) and per file
/// in a batch (the file is kept). On 6ea7520 the first test was persisted and the rest dropped.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class MultipleTestsTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new();

    public MultipleTestsTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        for (var i = 101; i <= 111; i++)
        {
            _ws.WriteTest("storage", $"TC-{i}", [Quotas]);
        }
    }

    public void Dispose() => _ws.Dispose();

    private TestCase Edited(string id) => _ws.Load("storage", id) with { ExpectedResult = $"Edited {id}" };

    private static string Array(params string[] objects) => "[\n" + string.Join(",\n", objects) + "\n]\n";

    private static string Json(TestCase test) => UpdatePromptCompiler.SerializeTest(test);

    private string TestFile(string id) => File.ReadAllText(_ws.PathOf($"test-cases/storage/{id}.md"));

    private static JsonElement Result(JsonElement batch, string id) =>
        batch.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("test").GetString() == id);

    private async Task<JsonElement> FailedAsync(string id)
    {
        var (_, output, _) = await _ws.IngestAllAsync();
        var result = Result(BaselineTestWorkspace.Json(output), id);
        Assert.Equal("failed", result.GetProperty("outcome").GetString());
        return result;
    }

    [Fact]
    public async Task SingleForm_TwoObjects_Exit5_NothingWritten()
    {
        var before = TestFile("TC-101");
        var from = _ws.PathOf(".spectra/edit.json");
        File.WriteAllText(from, Array(Json(Edited("TC-101")), Json(Edited("TC-102"))));

        var (exit, output, error) = await _ws.RunAsync(
            "ai", "ingest-update", "storage", "--test-id", "TC-101", "--from", from, "--output-format", "json");

        Assert.Equal(5, exit);
        Assert.Empty(output);
        var failure = BaselineTestWorkspace.Json(error);
        Assert.Equal("MULTIPLE_TESTS", failure.GetProperty("error_code").GetString());
        var message = failure.GetProperty("errors")[0].GetString()!;
        Assert.Contains("2 test objects", message);
        Assert.Contains("TC-101", message);
        Assert.Contains("TC-102", message);
        Assert.Equal(before, TestFile("TC-101"));
    }

    [Fact]
    public async Task Batch_ATenElementGroupArray_FailsAndIsKept_TheOtherFileIsWritten()
    {
        var ids = Enumerable.Range(101, 10).Select(i => $"TC-{i}").ToArray();
        var before = ids.ToDictionary(id => id, TestFile);
        _ws.StageRaw("storage", "TC-101", Array(ids.Select(id => Json(Edited(id))).ToArray()));
        _ws.StageEdit("storage", "TC-111", Edited("TC-111"));

        var (_, output, _) = await _ws.IngestAllAsync();

        var batch = BaselineTestWorkspace.Json(output);
        var failed = Result(batch, "TC-101");
        Assert.Equal("failed", failed.GetProperty("outcome").GetString());
        Assert.Equal("MULTIPLE_TESTS", failed.GetProperty("error_code").GetString());
        var message = failed.GetProperty("errors")[0].GetString()!;
        Assert.Contains("10 test objects", message);
        Assert.All(ids, id => Assert.Contains(id, message));
        Assert.All(ids, id => Assert.Equal(before[id], TestFile(id)));
        Assert.Equal("written", Result(batch, "TC-111").GetProperty("outcome").GetString());
        Assert.Contains("Edited TC-111", TestFile("TC-111"));
        Assert.Equal(["failed-TC-101.json"], _ws.StagedFiles("storage")); // Spec 090 (FR-B1): a failed file is kept as failed-<id>.json
    }

    [Fact]
    public async Task OneValidAndOneInvalidObject_CountsAsTwo()
    {
        _ws.StageRaw("storage", "TC-101", Array(Json(Edited("TC-101")), "{ \"id\": \"TC-102\" }"));

        var failed = await FailedAsync("TC-101");

        Assert.Equal("MULTIPLE_TESTS", failed.GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task TwoObjectsWithTheSameId_AreMultipleTests()
    {
        _ws.StageRaw("storage", "TC-101", Array(Json(Edited("TC-101")), Json(Edited("TC-101"))));

        var failed = await FailedAsync("TC-101");

        Assert.Equal("MULTIPLE_TESTS", failed.GetProperty("error_code").GetString());
        Assert.Contains("2 test objects", failed.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task AnObjectWithoutAnId_IsListedAsNoId()
    {
        _ws.StageRaw("storage", "TC-101", Array(Json(Edited("TC-101")), "{ \"title\": \"No id\" }"));

        var failed = await FailedAsync("TC-101");

        Assert.Contains("(no id)", failed.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task AnEmptyArray_StaysNoTests()
    {
        _ws.StageRaw("storage", "TC-101", "[]");

        var failed = await FailedAsync("TC-101");

        Assert.Equal("NO_TESTS", failed.GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task AGroupArrayStartingWithAnotherId_IsMultipleTests_NotIdMismatch()
    {
        var ids = Enumerable.Range(102, 10).Select(i => $"TC-{i}").ToArray();
        _ws.StageRaw("storage", "TC-101", Array(ids.Select(id => Json(Edited(id))).ToArray()));

        var failed = await FailedAsync("TC-101");

        Assert.Equal("MULTIPLE_TESTS", failed.GetProperty("error_code").GetString());
    }
}
