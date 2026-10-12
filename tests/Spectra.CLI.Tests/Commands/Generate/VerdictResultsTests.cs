using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 093 T1 (FR-016–FR-018): <c>ingest-verdict --suite S --all</c> returns the verdict it classified per test
/// (<c>results</c>) and takes <c>--ids</c> (unknown ids → <c>not_found</c>); counts and lists are unchanged; the single
/// <c>--from</c> form is unchanged. On 20ac624 the batch form reported counts only and had no <c>--ids</c>.
/// Workspace from the real <c>spectra init</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class VerdictResultsTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public VerdictResultsTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            _ws.WriteTest("storage", id, [Quotas]);
        }
    }

    public void Dispose() => _ws.Dispose();

    private async Task VerdictAsync(string id, string verdict)
    {
        var (exit, _, error) = await _ws.CompileCriticAsync("storage", id);
        Assert.True(exit == 0, error);
        _ws.WriteVerdict(id, verdict);
    }

    private static Dictionary<string, JsonElement> Results(JsonElement result) =>
        result.GetProperty("results").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetString()!, r => r);

    [Fact]
    public async Task TheBatch_ReturnsOneResultPerClassifiedTest_AndKeepsItsCounts()
    {
        await VerdictAsync("TC-101", "grounded");
        await VerdictAsync("TC-102", "partial");
        await VerdictAsync("TC-103", "hallucinated");

        var (exit, output, error) = await _ws.IngestVerdictAllAsync();

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(1, result.GetProperty("grounded").GetInt32());
        Assert.Equal(1, result.GetProperty("partial").GetInt32());
        Assert.Equal(1, result.GetProperty("hallucinated").GetInt32());
        var results = Results(result);
        Assert.Equal(3, results.Count);
        Assert.Equal("grounded", results["TC-101"].GetProperty("verdict").GetString());
        Assert.Equal("pass", results["TC-101"].GetProperty("gate").GetString());
        Assert.Equal("partial", results["TC-102"].GetProperty("verdict").GetString());
        Assert.Equal("pass", results["TC-102"].GetProperty("gate").GetString());
        Assert.Equal("hallucinated", results["TC-103"].GetProperty("verdict").GetString());
        Assert.Equal("drop", results["TC-103"].GetProperty("gate").GetString());
        Assert.Equal(0.9, results["TC-101"].GetProperty("score").GetDouble(), 3);
        Assert.Empty(result.GetProperty("not_found").EnumerateArray());
    }

    [Fact]
    public async Task Ids_LimitTheBatch_AndReportUnknownIds()
    {
        await VerdictAsync("TC-101", "grounded");
        await VerdictAsync("TC-102", "partial");

        var (exit, output, error) = await _ws.RunAsync(
            "ai", "ingest-verdict", "--suite", "storage", "--all", "--ids", "TC-102,TC-999", "--output-format", "json");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(new[] { "TC-102" }, Results(result).Keys.ToArray());
        Assert.Equal(new[] { "TC-999" }, result.GetProperty("not_found").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal(0, result.GetProperty("grounded").GetInt32());
        Assert.Equal(1, result.GetProperty("partial").GetInt32());
    }

    [Fact]
    public async Task StaleAndUnreadableVerdicts_HaveNoResult()
    {
        await VerdictAsync("TC-101", "grounded");
        File.WriteAllText(_ws.TestPath("storage", "TC-101"), File.ReadAllText(_ws.TestPath("storage", "TC-101")).Replace("It works", "It still works"));
        await VerdictAsync("TC-102", "grounded");
        File.WriteAllText(_ws.VerdictPath("TC-102"), "{not json");
        await VerdictAsync("TC-103", "partial");

        var (exit, output, error) = await _ws.IngestVerdictAllAsync();

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(new[] { "TC-103" }, Results(result).Keys.ToArray());
        Assert.Contains("TC-101", result.GetProperty("stale").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, result.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task IdsWithoutAll_IsAUsageError()
    {
        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-verdict", "--ids", "TC-101", "--output-format", "json");

        Assert.Equal(1, exit);
        Assert.Equal("--ids is only valid with --all.", error.TrimEnd('\r', '\n'));
        Assert.Equal("", output);
    }

    [Fact]
    public async Task TheSingleForm_IsUnchanged()
    {
        // Guard: passes on 20ac624 and after.
        await VerdictAsync("TC-101", "partial");

        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-verdict", "--from", _ws.VerdictPath("TC-101"), "--output-format", "json");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal("partial", result.GetProperty("verdict").GetString());
        Assert.False(result.TryGetProperty("results", out _));
    }
}
