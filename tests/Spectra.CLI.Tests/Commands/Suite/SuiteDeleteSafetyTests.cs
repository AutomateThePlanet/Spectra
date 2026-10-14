using System.Security.Cryptography;
using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.IdAllocation;

namespace Spectra.CLI.Tests.Commands.Suite;

/// <summary>
/// Spec 096 F2 (FR-004, FR-005; analyze M2): <c>spectra suite delete</c> removes dependencies in other suites exactly as
/// <c>spectra delete</c> does since 090 — single-value <c>depends_on</c> cleared losslessly, list or unparseable ones
/// warned, no other byte changed — and removes the suite's leftovers (staging, generation plan, verdict files). It
/// never touches baselines or takes the baseline lock. On 23aa08c a regex rewrote every other suite's files (removing
/// indented <c>- &lt;id&gt;</c> lines and collapsing blank lines) and the leftovers stayed.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SuiteDeleteSafetyTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose()
    {
        BaselineStore.LockTimeoutOverride = null;
        _ws.Dispose();
    }

    private static string Markdown(string id, string extraFrontmatter = "", string extraBody = "", string newline = "\n") =>
        ("---\n" +
         $"id: {id}\n" +
         "priority: high\n" +
         extraFrontmatter +
         "source_refs:\n  - \"docs/storage.md#Quotas\"\n" +
         "---\n\n" +
         $"# A test {id}\n\n" +
         "## Steps\n\n1. Do the thing\n" + extraBody + "\n" +
         "## Expected Result\n\nIt works\n").Replace("\n", newline);

    private void Write(string suite, string id, string content)
    {
        var path = _ws.PathOf($"test-cases/{suite}/{id}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void WriteFile(string relative, string content)
    {
        var path = _ws.PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static readonly string[] Leftovers =
    [
        ".spectra/updates/a/updated-TC-101.json",
        ".spectra/repairs/a/failed-TC-102.json",
        ".spectra/generation-plan/a.json",
        ".spectra/verdicts/critic-verdict-TC-101.json",
        ".spectra/verdicts/critic-input-TC-101.json",
    ];

    private void Arrange()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 30GB.\n");
        Write("a", "TC-101", Markdown("TC-101"));
        Write("a", "TC-102", Markdown("TC-102"));
        Write("b", "TC-201", Markdown("TC-201", "depends_on: TC-101\n"));
        Write("b", "TC-202", Markdown("TC-202", "depends_on:\n  - TC-101\n"));
        Write("b", "TC-203", Markdown("TC-203", extraBody: "  - TC-101\n\n\n\nMore steps.\n"));
        Write("c", "TC-301", Markdown("TC-301", extraBody: "\n\n\nTail.\n", newline: "\r\n"));
        foreach (var suite in new[] { "a", "b", "c" })
        {
            _ws.RebuildIndex(suite);
        }

        foreach (var file in Leftovers)
        {
            WriteFile(file, "{}");
        }

        WriteFile("test-cases/b/_source-baseline.json", "{\"suite\":\"b\",\"tests\":{}}\n");
        WriteFile("test-cases/c/_source-baseline.json", "{\"suite\":\"c\",\"tests\":{}}\n");
    }

    private Task<(int Exit, string Out, string Err)> SuiteDeleteAsync(params string[] extra) =>
        _ws.RunAsync(["suite", "delete", "a", .. extra, "--no-interaction", "--output-format", "json"]);

    private Dictionary<string, string> HashTree() =>
        Directory.EnumerateFiles(_ws.Root, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".spectra-result.json", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(_ws.Root, f).Replace('\\', '/'),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    private byte[] Bytes(string relative) => File.ReadAllBytes(_ws.PathOf(relative));

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString()).ToArray();

    [Fact]
    public async Task TheDryRun_ListsDependentsWarningsAndLeftovers_AndChangesNothing()
    {
        Arrange();
        var before = HashTree();

        var (exit, output, error) = await SuiteDeleteAsync("--dry-run", "--force");

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Contains(result.GetProperty("dependents_cleaned").EnumerateArray(), d => d.GetProperty("id").GetString() == "TC-201");
        Assert.Contains(Strings(result.GetProperty("warnings")), w => w.Contains("TC-202"));
        // Spec 098 F3 (deliberate change): verdict and record files are listed under verdict_files_removed, not leftovers.
        var leftovers = Strings(result.GetProperty("leftovers")).Concat(Strings(result.GetProperty("verdict_files_removed"))).ToList();
        foreach (var file in Leftovers)
        {
            Assert.Contains(leftovers, l => file.StartsWith(l.TrimEnd('/'), StringComparison.Ordinal));
        }

        Assert.DoesNotContain(Strings(result.GetProperty("leftovers")), l => l.StartsWith(".spectra/verdicts/", StringComparison.Ordinal));

        Assert.Equal(before, HashTree());
    }

    [Fact]
    public async Task ForceDelete_CleansTheScalarDependentLosslessly_AndTouchesNothingElse()
    {
        Arrange();
        var tc201 = File.ReadAllText(_ws.PathOf("test-cases/b/TC-201.md"));
        var tc202 = Bytes("test-cases/b/TC-202.md");
        var tc203 = Bytes("test-cases/b/TC-203.md");
        var tc301 = Bytes("test-cases/c/TC-301.md");

        var (exit, _, error) = await SuiteDeleteAsync("--force");

        Assert.True(exit == 0, error);
        Assert.Equal(tc201.Replace("depends_on: TC-101\n", ""), File.ReadAllText(_ws.PathOf("test-cases/b/TC-201.md")));
        Assert.Equal(tc202, Bytes("test-cases/b/TC-202.md"));
        Assert.Equal(tc203, Bytes("test-cases/b/TC-203.md"));
        Assert.Equal(tc301, Bytes("test-cases/c/TC-301.md"));
        Assert.False(Directory.Exists(_ws.PathOf("test-cases/a")));
    }

    [Fact]
    public async Task ForceDelete_RemovesTheSuitesLeftovers()
    {
        Arrange();

        var (exit, output, error) = await SuiteDeleteAsync("--force");

        Assert.True(exit == 0, error);
        foreach (var file in Leftovers)
        {
            Assert.False(File.Exists(_ws.PathOf(file)), file);
        }

        Assert.False(Directory.Exists(_ws.PathOf(".spectra/updates/a")));
        Assert.False(Directory.Exists(_ws.PathOf(".spectra/repairs/a")));
        Assert.NotEmpty(BaselineTestWorkspace.Json(output).GetProperty("leftovers_removed").EnumerateArray());
    }

    [Fact]
    public async Task ForceDelete_TakesNoBaselineLock_AndLeavesOtherBaselinesAlone()
    {
        Arrange();
        var b = Bytes("test-cases/b/_source-baseline.json");
        var c = Bytes("test-cases/c/_source-baseline.json");
        BaselineStore.LockTimeoutOverride = TimeSpan.FromMilliseconds(200);
        using var held = await FileLockHandle.AcquireAsync(BaselineStore.LockPath(_ws.Root), TimeSpan.FromSeconds(5), CancellationToken.None);

        var (exit, _, error) = await SuiteDeleteAsync("--force");

        Assert.True(exit == 0, error);
        Assert.Equal(b, Bytes("test-cases/b/_source-baseline.json"));
        Assert.Equal(c, Bytes("test-cases/c/_source-baseline.json"));
    }

    [Fact]
    public async Task WithoutForce_ExternalDependentsStillRefuse()
    {
        Arrange();
        var before = HashTree();

        var (exit, output, _) = await SuiteDeleteAsync();

        Assert.NotEqual(0, exit);
        Assert.Contains("EXTERNAL_DEPENDENCIES", output);
        Assert.Equal(before, HashTree());
    }
}
