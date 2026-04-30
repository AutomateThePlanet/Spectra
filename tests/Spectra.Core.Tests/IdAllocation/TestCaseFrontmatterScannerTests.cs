using Spectra.Core.IdAllocation;

namespace Spectra.Core.Tests.IdAllocation;

public sealed class TestCaseFrontmatterScannerTests : IDisposable
{
    private readonly string _root;
    private readonly string _testCasesDir;

    public TestCaseFrontmatterScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"spectra-scan-{Guid.NewGuid():N}");
        _testCasesDir = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testCasesDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task GetMaxIdNumberAsync_MissingDir_ReturnsZero()
    {
        var missing = Path.Combine(_root, "does-not-exist");
        var scanner = new TestCaseFrontmatterScanner(missing);
        Assert.Equal(0, await scanner.GetMaxIdNumberAsync("TC"));
    }

    [Fact]
    public async Task GetMaxIdNumberAsync_EmptyDir_ReturnsZero()
    {
        var scanner = new TestCaseFrontmatterScanner(_testCasesDir);
        Assert.Equal(0, await scanner.GetMaxIdNumberAsync("TC"));
    }

    [Fact]
    public async Task GetMaxIdNumberAsync_FindsMaxAcrossSuites()
    {
        WriteTest("checkout", "TC-100");
        WriteTest("auth", "TC-247");
        WriteTest("orders", "TC-150");

        var scanner = new TestCaseFrontmatterScanner(_testCasesDir);
        Assert.Equal(247, await scanner.GetMaxIdNumberAsync("TC"));
    }

    [Fact]
    public async Task GetMaxIdNumberAsync_IgnoresPrefixMismatch()
    {
        WriteTest("checkout", "TC-100");
        WriteTest("checkout", "BUG-999");

        var scanner = new TestCaseFrontmatterScanner(_testCasesDir);
        Assert.Equal(100, await scanner.GetMaxIdNumberAsync("TC"));
    }

    [Fact]
    public async Task GetMaxIdNumberAsync_SkipsMalformedFrontmatter()
    {
        WriteTest("checkout", "TC-100");
        File.WriteAllText(Path.Combine(_testCasesDir, "checkout", "broken.md"), "no frontmatter here");

        var scanner = new TestCaseFrontmatterScanner(_testCasesDir);
        Assert.Equal(100, await scanner.GetMaxIdNumberAsync("TC"));
    }

    [Fact]
    public async Task EnumerateAllIdsAsync_ReturnsEveryId()
    {
        WriteTest("checkout", "TC-100");
        WriteTest("auth", "TC-247");
        WriteTest("auth", "TC-100"); // duplicate intentional

        var scanner = new TestCaseFrontmatterScanner(_testCasesDir);
        var all = await scanner.EnumerateAllIdsAsync();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, p => p.Id == "TC-100" && p.File.Contains("checkout"));
        Assert.Contains(all, p => p.Id == "TC-247");
    }

    [Fact]
    public async Task GetMaxIdNumberAsync_SkipsUnderscoredFiles()
    {
        WriteTest("checkout", "TC-100");
        var indexPath = Path.Combine(_testCasesDir, "checkout", "_other.md");
        File.WriteAllText(indexPath, "---\nid: TC-9999\n---\nbody");

        var scanner = new TestCaseFrontmatterScanner(_testCasesDir);
        Assert.Equal(100, await scanner.GetMaxIdNumberAsync("TC"));
    }

    private void WriteTest(string suite, string id)
    {
        var dir = Path.Combine(_testCasesDir, suite);
        Directory.CreateDirectory(dir);
        var content = $"""
            ---
            id: {id}
            title: {id}
            suite: {suite}
            ---

            # {id}

            Body
            """;
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), content);
    }
}
