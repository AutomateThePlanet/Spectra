using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 090 T6 (FR-B1, 089-review finding 1): a staged file that fails in <c>ingest-update --all</c> is renamed to
/// <c>failed-&lt;id&gt;.json</c>, which no batch ever reads again; a later success for the id removes it. Both staging
/// folders. On fc82b7a the failed file kept its <c>updated-</c> / <c>repaired-</c> name and failed again in every later
/// batch.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class FailedStagingTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";
    private const string Malformed = "[\n  { \"id\": \"TC-101\", \"title\": }\n]\n";
    private const string MalformedToo = "[\n  { \"id\": \"TC-101\", \"steps\": }\n]\n";

    private readonly BaselineTestWorkspace _ws = new();

    public FailedStagingTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        _ws.WriteTest("storage", "TC-102", [Quotas]);
        _ws.InitBaselineAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    // Folder-neutral helpers: repairs = true → .spectra/repairs/ with --keep-baseline.
    private string Folder(bool repairs) => repairs ? ".spectra/repairs/storage" : ".spectra/updates/storage";

    private string Prefix(bool repairs) => repairs ? "repaired-" : "updated-";

    private void StageRaw(bool repairs, string id, string content)
    {
        if (repairs) _ws.StageRepairRaw("storage", id, content);
        else _ws.StageRaw("storage", id, content);
    }

    private void StageTest(bool repairs, string id, Spectra.Core.Models.TestCase test)
    {
        if (repairs) _ws.StageRepair("storage", id, test);
        else _ws.StageEdit("storage", id, test);
    }

    private string[] Files(bool repairs) => repairs ? _ws.RepairFiles("storage") : _ws.StagedFiles("storage");

    private Task<(int Exit, string Out, string Err)> IngestAsync(bool repairs) =>
        repairs ? _ws.IngestAllAsync("storage", "--keep-baseline") : _ws.IngestAllAsync();

    private string FailedPath(bool repairs, string id) => _ws.PathOf($"{Folder(repairs)}/failed-{id}.json");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFailedFile_IsRenamedToFailed_AndNeverReadAgain(bool repairs)
    {
        StageRaw(repairs, "TC-101", Malformed);

        var (exit, output, _) = await IngestAsync(repairs);

        Assert.Equal(8, exit);
        var result = Assert.Single(BaselineTestWorkspace.Json(output).GetProperty("results").EnumerateArray());
        Assert.Equal($"{Folder(repairs)}/failed-TC-101.json", result.GetProperty("file").GetString());
        Assert.Equal(["failed-TC-101.json"], Files(repairs));

        var (againExit, again, _) = await IngestAsync(repairs);

        Assert.Equal(0, againExit);
        Assert.Empty(BaselineTestWorkspace.Json(again).GetProperty("results").EnumerateArray());
        Assert.Equal(["failed-TC-101.json"], Files(repairs));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANewFailure_ReplacesAnOlderFailedFile(bool repairs)
    {
        Directory.CreateDirectory(_ws.PathOf(Folder(repairs)));
        File.WriteAllText(FailedPath(repairs, "TC-101"), "old failure");
        StageRaw(repairs, "TC-101", MalformedToo);

        var (exit, _, _) = await IngestAsync(repairs);

        Assert.Equal(8, exit);
        Assert.Equal(MalformedToo, File.ReadAllText(FailedPath(repairs, "TC-101")));
        Assert.Equal(["failed-TC-101.json"], Files(repairs));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALaterWrittenEdit_RemovesTheFailedFile(bool repairs)
    {
        Directory.CreateDirectory(_ws.PathOf(Folder(repairs)));
        File.WriteAllText(FailedPath(repairs, "TC-101"), Malformed);
        StageTest(repairs, "TC-101", _ws.Load("storage", "TC-101") with { ExpectedResult = "Edited" });

        var (exit, output, error) = await IngestAsync(repairs);

        Assert.True(exit == 0, error);
        Assert.Equal("written", BaselineTestWorkspace.Json(output).GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Empty(Files(repairs));
    }

    [Fact]
    public async Task ALaterUnchangedEdit_RemovesTheFailedFile()
    {
        Directory.CreateDirectory(_ws.PathOf(Folder(false)));
        File.WriteAllText(FailedPath(false, "TC-101"), Malformed);
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
        Assert.Equal("unchanged", BaselineTestWorkspace.Json(output).GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Empty(_ws.StagedFiles("storage"));
    }

    [Fact]
    public async Task ALaterUnchangedPendingEdit_RemovesTheFailedFile()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
        Directory.CreateDirectory(_ws.PathOf(Folder(false)));
        File.WriteAllText(FailedPath(false, "TC-101"), Malformed);
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101"));

        var (exit, output, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
        Assert.Equal("unchanged_pending", BaselineTestWorkspace.Json(output).GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Empty(_ws.StagedFiles("storage"));
    }

    [Fact]
    public async Task AFailedFileOfAnotherTest_IsLeftAlone()
    {
        Directory.CreateDirectory(_ws.PathOf(Folder(false)));
        File.WriteAllText(FailedPath(false, "TC-102"), Malformed);
        _ws.StageEdit("storage", "TC-101", _ws.Load("storage", "TC-101") with { ExpectedResult = "Edited" });

        var (exit, _, error) = await _ws.IngestAllAsync();

        Assert.True(exit == 0, error);
        Assert.Equal(["failed-TC-102.json"], _ws.StagedFiles("storage"));
    }
}
