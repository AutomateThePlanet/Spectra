using System.Diagnostics;
using System.Text;
using Spectra.CLI.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 084 SC-003 / SC-004 (research R13): <c>update-triage --all-suites</c> on a 2,500-test workspace finishes in
/// under 30 s, and with about 10% of tests affected its stdout stays within the ~2K-token budget (8,000 characters).
/// The workspace models features: each suite owns five docs (an Overview and five parts each), each test references
/// one part and its doc's Overview, and links one criterion; a doc edit changes every part of one doc in five suites.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateTriagePerformanceTests(ITestOutputHelper output) : IDisposable
{
    private const int Suites = 10;
    private const int TestsPerSuite = 250;
    private const int DocsPerSuite = 5;
    private const int PartsPerDoc = 5;

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static string Doc(int suite, int doc, int version) =>
        new StringBuilder()
            .Append($"# Feature {suite}-{doc}\n\n## Overview\n\nThe feature {suite}-{doc} lets users manage records.\n\n")
            .Append(string.Concat(Enumerable.Range(1, PartsPerDoc).Select(p =>
                $"## Part {p}\n\nUsers can store up to {(version == 0 ? 10 : 20) * p} records in part {p}. "
                + "Each record keeps its owner, its title and its history of changes.\n\n")))
            .ToString();

    private void Generate()
    {
        var criteria = new StringBuilder("criteria:\n");
        for (var c = 0; c < 50; c++)
        {
            criteria.Append($"  - id: AC-PERF-{c:D3}\n    text: \"Users MUST be able to manage record set {c}\"\n    source_type: document\n");
        }

        _ws.WriteCriteriaFile("docs/perf.criteria.yaml", criteria.ToString());

        var id = 1000;
        for (var s = 0; s < Suites; s++)
        {
            for (var d = 0; d < DocsPerSuite; d++)
            {
                _ws.WriteDoc($"docs/suite-{s:D2}/doc-{d}.md", Doc(s, d, 0));
            }

            var dir = _ws.PathOf($"test-cases/suite-{s:D2}");
            Directory.CreateDirectory(dir);
            for (var t = 0; t < TestsPerSuite; t++)
            {
                var part = t % (DocsPerSuite * PartsPerDoc);
                var doc = $"docs/suite-{s:D2}/doc-{part / PartsPerDoc}.md";
                var testId = $"TC-{id++}";
                File.WriteAllText(Path.Combine(dir, testId + ".md"), BaselineTestWorkspace.TestMarkdown(
                    testId, [$"{doc}#Part-{part % PartsPerDoc + 1}", $"{doc}#Overview"], [$"AC-PERF-{t % 50:D3}"]));
            }

            _ws.RebuildIndex($"suite-{s:D2}");
        }
    }

    [Fact]
    public async Task AllSuitesTriage_On2500Tests_IsFast_AndCompact()
    {
        var setup = Stopwatch.StartNew();
        Generate();
        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        for (var s = 0; s < 5; s++)
        {
            _ws.WriteDoc($"docs/suite-{s:D2}/doc-0.md", Doc(s, 0, 1)); // every part of doc 0 changes a number
        }

        setup.Stop();
        Assert.True(setup.Elapsed < TimeSpan.FromSeconds(60), $"setup took {setup.Elapsed}");

        var timer = Stopwatch.StartNew();
        var (exit, stdout, stderr) = await _ws.RunAsync("ai", "update-triage", "--all-suites", "--output-format", "json");
        timer.Stop();

        output.WriteLine($"setup {setup.Elapsed}, triage {timer.Elapsed}, stdout {stdout.Length} chars");
        Assert.True(exit == 0, stderr);
        var triage = BaselineTestWorkspace.Json(stdout);
        var affected = triage.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("tests").GetArrayLength());
        Assert.Equal(TestsPerSuite, affected); // 10% of 2,500
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(30), $"triage took {timer.Elapsed}");
        Assert.True(stdout.Length <= 8000, $"stdout is {stdout.Length} characters");
    }
}
