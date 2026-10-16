using Spectra.CLI.Baseline;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Baseline;

/// <summary>
/// Spec 099 F1 (FR-005): the scan an ingest derives from the suite it read before persisting, plus the files it wrote,
/// equals the scan <see cref="BaselineStore.ScanSuiteAsync"/> makes of the disk afterwards — tests, IDs, unparseable
/// files and their order — so pruning and its warning are unchanged.
/// </summary>
public sealed class SuiteScanBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"spectra-099-scan-{Guid.NewGuid():N}");
    private string TestsPath => Path.Combine(_root, "test-cases");
    private string SuitePath => Path.Combine(TestsPath, "s");

    public SuiteScanBuilderTests() => Directory.CreateDirectory(SuitePath);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Test(string id, string title = "A test") =>
        $"---\nid: {id}\npriority: high\n---\n\n# {title} {id}\n\n## Steps\n\n1. Do it\n\n## Expected Result\n\nIt works\n";

    private const string Unparseable = "---\nid: TC-9\npriority: high\n---\n\nNo title.\n";

    private void Disk(string file, string text) => File.WriteAllText(Path.Combine(SuitePath, file), text);

    /// <summary>Reads the suite, applies the writes on disk and in the builder, and compares with a fresh scan.</summary>
    private async Task AssertSameAsRescanAsync(params (string File, string Text)[] writes)
    {
        var before = await SuiteScanBuilder.ReadAsync(SuitePath, captureLayout: false, CancellationToken.None);

        var written = new List<SuiteFile>();
        foreach (var (file, text) in writes)
        {
            var path = Path.Combine(SuitePath, file);
            File.WriteAllText(path, text);
            var parsed = new TestCaseParser().Parse(text, file);
            Assert.True(parsed.IsSuccess, "a written test always parses");
            written.Add(new SuiteFile(path, parsed.Value!));
        }

        var derived = SuiteScanBuilder.Build(_root, before, written);
        var rescan = await BaselineStore.ScanSuiteAsync(_root, TestsPath, "s", CancellationToken.None);

        // The baseline reads the IDs, AllParsed and the unparseable files (BaselineStore.SaveSuiteAsync), never a
        // test's FilePath — an update keeps its in-memory FilePath — so the tests are compared by ID and order.
        Assert.Equal(rescan.Tests.Select(t => t.Id), derived.Tests.Select(t => t.Id));
        Assert.Equal(rescan.Ids.OrderBy(i => i, StringComparer.Ordinal), derived.Ids.OrderBy(i => i, StringComparer.Ordinal));
        Assert.Equal(rescan.UnparseableFiles, derived.UnparseableFiles);
        Assert.Equal(rescan.AllParsed, derived.AllParsed);
    }

    [Fact]
    public async Task NewTestAdded()
    {
        Disk("TC-1.md", Test("TC-1"));
        Disk("TC-5.md", Unparseable);
        await AssertSameAsRescanAsync(("TC-2.md", Test("TC-2")));
    }

    [Fact]
    public async Task ExistingTestRewritten()
    {
        Disk("TC-1.md", Test("TC-1"));
        Disk("TC-2.md", Test("TC-2"));
        await AssertSameAsRescanAsync(("TC-1.md", Test("TC-1", "Edited")));
    }

    [Fact]
    public async Task UnparseableFileOverwrittenByAValidTest()
    {
        Disk("TC-1.md", Test("TC-1"));
        Disk("TC-9.md", Unparseable);
        Disk("TC-3.md", Unparseable);
        await AssertSameAsRescanAsync(("TC-9.md", Test("TC-9")));
    }

    [Fact]
    public async Task FileHoldingAnotherIdOverwritten()
    {
        Disk("TC-1.md", Test("TC-7"));
        Disk("TC-2.md", Test("TC-2"));
        await AssertSameAsRescanAsync(("TC-1.md", Test("TC-1")));
    }

    [Fact]
    public async Task TestStoredUnderAnotherNameUpdated_LeavesTheOldFile()
    {
        // An update writes {id}.md; the old foo.md stays (existing behaviour).
        Disk("foo.md", Test("TC-1"));
        Disk("TC-2.md", Test("TC-2"));
        await AssertSameAsRescanAsync(("TC-1.md", Test("TC-1", "Edited")));
    }

    [Fact]
    public async Task CaseVariantOverwrite_FollowsTheFileSystem()
    {
        Disk("tc-1.md", Test("tc-1"));
        Disk("TC-2.md", Unparseable);
        await AssertSameAsRescanAsync(("TC-1.md", Test("TC-1")));
    }

    [Fact]
    public async Task UnderscoreFilesAndSubfoldersAreIgnored()
    {
        Disk("TC-1.md", Test("TC-1"));
        Disk("_notes.md", Unparseable);
        Directory.CreateDirectory(Path.Combine(SuitePath, "nested"));
        File.WriteAllText(Path.Combine(SuitePath, "nested", "TC-8.md"), Test("TC-8"));
        await AssertSameAsRescanAsync(("TC-3.md", Test("TC-3")));
    }

    [Fact]
    public async Task MissingSuiteFolder_FirstWriteCreatesIt()
    {
        Directory.Delete(SuitePath);
        var before = await SuiteScanBuilder.ReadAsync(SuitePath, captureLayout: false, CancellationToken.None);
        Assert.Empty(before);

        Directory.CreateDirectory(SuitePath);
        var path = Path.Combine(SuitePath, "TC-1.md");
        File.WriteAllText(path, Test("TC-1"));
        var derived = SuiteScanBuilder.Build(_root, before, [new SuiteFile(path, new TestCaseParser().Parse(Test("TC-1"), "TC-1.md").Value!)]);
        var rescan = await BaselineStore.ScanSuiteAsync(_root, TestsPath, "s", CancellationToken.None);

        Assert.Equal(rescan.Ids, derived.Ids);
        Assert.Empty(derived.UnparseableFiles);
    }

    [Fact]
    public async Task NothingWritten_EqualsTheScan()
    {
        Disk("TC-1.md", Test("TC-1"));
        Disk("TC-4.md", Unparseable);
        await AssertSameAsRescanAsync();
    }
}
