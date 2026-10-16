using Spectra.CLI.Generation;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Diagnostics;

namespace Spectra.CLI.Tests.Baseline;

/// <summary>
/// Spec 099 F1 (FR-006, SC-002): one ingest call parses each of the suite's N test files once (each parse attempt
/// counts, an unparseable file included). Before 099 the baseline record parsed the suite a second time (2N).
/// Counted with a <see cref="ParseCounter"/> scope around the call, so parallel tests never mix in.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestParseCountTests : IDisposable
{
    private const string Suite = "storage";
    private const string Quotas = "docs/storage.md#Quotas";
    private const string Sharing = "docs/storage.md#Sharing";

    /// <summary>TC-101…TC-104 and one unparseable file.</summary>
    private const int SuiteFiles = 5;

    private readonly BaselineTestWorkspace _ws = new();

    public IngestParseCountTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB**.\n\n## Sharing\n\nLinks expire after 7 days.\n");
        _ws.WriteTest(Suite, "TC-101", [Quotas]);
        _ws.WriteTest(Suite, "TC-102", [Quotas]);
        _ws.WriteTest(Suite, "TC-103", [Sharing]);
        _ws.WriteTest(Suite, "TC-104", [Sharing]);
        File.WriteAllText(_ws.PathOf($"test-cases/{Suite}/TC-109.md"), "---\nid: TC-109\npriority: high\n---\n\nNo title.\n");
        _ws.InitBaselineAsync(Suite).GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private async Task<int> CountAsync(params string[] args)
    {
        using var scope = ParseCounter.Begin();
        var (exit, output, error) = await _ws.RunAsync([.. args, "--output-format", "json"]);
        Assert.True(exit is 0, $"exit {exit}\nstdout: {output}\nstderr: {error}");
        return scope.FullParses;
    }

    private string EditFile(string id, string expected)
    {
        var path = _ws.PathOf($".spectra/edit-{id}.json");
        File.WriteAllText(path, "[\n" + UpdatePromptCompiler.SerializeTest(_ws.Load(Suite, id) with { ExpectedResult = expected }) + "\n]\n");
        return path;
    }

    private void StageTwoEdits()
    {
        _ws.StageEdit(Suite, "TC-101", _ws.Load(Suite, "TC-101") with { ExpectedResult = "Quota enforced" });
        _ws.StageEdit(Suite, "TC-103", _ws.Load(Suite, "TC-103") with { ExpectedResult = "Link expired" });
    }

    [Fact]
    public async Task IngestTests_ParsesEachSuiteFileOnce()
    {
        var payload = _ws.GeneratedPayload(("TC-200", [Quotas], []), ("TC-201", [Sharing], []));

        Assert.Equal(SuiteFiles, await CountAsync("ai", "ingest-tests", Suite, "--from", payload));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IngestUpdateTestId_ParsesEachSuiteFileOnce(bool keepBaseline)
    {
        string[] args = ["ai", "ingest-update", Suite, "--test-id", "TC-101", "--from", EditFile("TC-101", "Quota enforced")];

        Assert.Equal(SuiteFiles, await CountAsync(keepBaseline ? [.. args, "--keep-baseline"] : args));
    }

    [Fact]
    public async Task IngestUpdateTestId_UnchangedEdit_ParsesEachSuiteFileOnce()
    {
        // An unchanged edit runs the 089 pending-change check before recording the review.
        Assert.Equal(SuiteFiles, await CountAsync(
            "ai", "ingest-update", Suite, "--test-id", "TC-101", "--from", EditFile("TC-101", "It works")));
    }

    [Fact]
    public async Task IngestUpdateTestId_DryRun_ParsesEachSuiteFileOnce() =>
        Assert.Equal(SuiteFiles, await CountAsync(
            "ai", "ingest-update", Suite, "--test-id", "TC-101", "--from", EditFile("TC-101", "Quota enforced"), "--dry-run"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IngestUpdateAll_ParsesEachSuiteFileOnce(bool keepBaseline)
    {
        if (keepBaseline)
        {
            _ws.StageRepair(Suite, "TC-101", _ws.Load(Suite, "TC-101") with { ExpectedResult = "Quota enforced" });
            _ws.StageRepair(Suite, "TC-103", _ws.Load(Suite, "TC-103") with { ExpectedResult = "Link expired" });
        }
        else
        {
            StageTwoEdits();
        }

        string[] args = ["ai", "ingest-update", Suite, "--all"];
        Assert.Equal(SuiteFiles, await CountAsync(keepBaseline ? [.. args, "--keep-baseline"] : args));
    }

    [Fact]
    public async Task IngestUpdateAll_DryRun_ParsesEachSuiteFileOnce()
    {
        StageTwoEdits();

        Assert.Equal(SuiteFiles, await CountAsync("ai", "ingest-update", Suite, "--all", "--dry-run"));
    }
}
