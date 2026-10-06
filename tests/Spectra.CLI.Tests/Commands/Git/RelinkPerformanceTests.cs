using System.Diagnostics;
using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 SC-001 (and analysis H1): 500 doc files renamed in one commit, 2,500 tests in 10 suites (each doc referenced
/// by 5 tests) — half baselined before the rename, half never. <c>relink-sources --all-suites --dry-run</c> finishes
/// in under 30 s with one rename scan for the one owner, and triage lists every test as RELINK.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class RelinkPerformanceTests(Xunit.Abstractions.ITestOutputHelper output) : IDisposable
{
    private const int Docs = 500;
    private const int Suites = 10;
    private const int TestsPerSuite = 250;

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose()
    {
        GitAccess.RunnerOverride = null;
        _ws.Dispose();
    }

    private void WriteSuite(int suite)
    {
        var name = $"s{suite:D2}";
        Directory.CreateDirectory(_ws.PathOf($"test-cases/{name}"));
        for (var i = 0; i < TestsPerSuite; i++)
        {
            var n = (suite - 1) * TestsPerSuite + i; // 0..2499
            var doc = n % Docs;
            var id = $"TC-{n + 1:D4}";
            File.WriteAllText(_ws.PathOf($"test-cases/{name}/{id}.md"),
                BaselineTestWorkspace.TestMarkdown(id, [$"docs/old/d{doc:D3}.md#Section"]));
        }

        _ws.RebuildIndex(name);
    }

    [GitFact]
    public async Task FiveHundredRenamedDocs_TwentyFiveHundredTests_RelinkInUnder30s_OneScan()
    {
        _ws.InitGit();
        for (var d = 0; d < Docs; d++)
        {
            _ws.WriteDoc($"docs/old/d{d:D3}.md", $"# Doc {d}\n\n## Section\n\nDocument number {d} says {d * 7} items.\n");
        }

        for (var s = 1; s <= Suites / 2; s++) WriteSuite(s);
        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        for (var s = Suites / 2 + 1; s <= Suites; s++) WriteSuite(s); // never baselined: no entries
        GitFixture.Commit(_ws.Root, "docs and tests");
        GitFixture.Git(_ws.Root, "mv", "docs/old", "docs/new");
        GitFixture.Commit(_ws.Root, "restructure: 500 files renamed in one commit");

        var runner = new RecordingGitRunner();
        GitAccess.RunnerOverride = runner;
        var clock = Stopwatch.StartNew();
        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--all-suites", "--dry-run");
        clock.Stop();
        output.WriteLine($"relink-sources --all-suites --dry-run: {clock.Elapsed.TotalSeconds:0.00} s, {runner.Calls.Count} git call(s)");

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"relink-sources took {clock.Elapsed.TotalSeconds:0.0} s");
        Assert.Equal(1, runner.RenameScans);
        Assert.Equal(Suites * TestsPerSuite, relink.GetProperty("counts").GetProperty("tests_relinked").GetInt32());
        Assert.Equal(0, relink.GetProperty("counts").GetProperty("refs_not_relinked").GetInt32());

        var triage = await _ws.RunJsonAsync("ai", "update-triage", "--all-suites", "--dry-run");
        foreach (var suite in triage.GetProperty("suites").EnumerateArray())
        {
            var counts = suite.GetProperty("counts");
            Assert.Equal(TestsPerSuite, counts.GetProperty("relink").GetInt32());
            Assert.False(counts.TryGetProperty("unbaselined", out _), $"{suite}: no test is counted twice");
        }

        Assert.Empty(triage.GetProperty("unbaselined").EnumerateArray());
    }
}
