using System.Diagnostics;
using Spectra.Core.Validation;
using Xunit.Abstractions;

namespace Spectra.Core.Tests.Validation;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TitleCoveragePerformanceCollection
{
    public const string Name = "TitleCoveragePerformance";
}

/// <summary>
/// Spec 095 T1 performance (FR-005, SC-004): coverage for 100 behaviours against 2,500 existing tests is decided in
/// under 2 seconds. Runs without parallel neighbours so the timing is the rule's own. On db20415 there was no rule.
/// </summary>
[Collection(TitleCoveragePerformanceCollection.Name)]
public sealed class TitleCoveragePerformanceTests(ITestOutputHelper output)
{
    private static readonly string[] Words =
    [
        "upload", "quota", "file", "share", "link", "expires", "after", "days", "folder", "nested", "trash", "restore",
        "version", "password", "locked", "minutes", "viewer", "editor", "revoke", "session", "settings", "rounded",
        "decimal", "banner", "warning", "refused", "succeeds", "exactly", "larger", "than", "limit", "name", "rename",
        "admin", "account", "export", "import", "report", "audit", "log", "retention", "policy", "delete", "confirm",
    ];

    private static string Title(Random random) =>
        string.Join(" ", Enumerable.Range(0, random.Next(5, 11)).Select(_ => Words[random.Next(Words.Length)]));

    [Fact]
    public void HundredBehaviours_AgainstTwentyFiveHundredTests_TakeUnderTwoSeconds()
    {
        var random = new Random(2026);
        var tests = Enumerable.Range(1, 2500).Select(i => ($"TC-{i:D4}", Title(random))).ToList();
        var behaviours = Enumerable.Range(0, 100)
            .Select(i => i % 10 == 0 ? tests[random.Next(tests.Count)].Item2 + " now" : Title(random))
            .ToList();

        TitleCoverage.Match(behaviours.Take(5).ToList(), tests.Take(50).ToList(), 0.6); // warm-up (JIT)

        // The rule is deterministic, so the fastest of five runs is its own time; slower runs only measure other test
        // assemblies sharing the machine (dotnet test runs the projects in parallel).
        IReadOnlyList<CoverageMatch?> matches = [];
        var times = new List<TimeSpan>();
        for (var run = 0; run < 5; run++)
        {
            var timer = Stopwatch.StartNew();
            matches = TitleCoverage.Match(behaviours, tests, 0.6);
            timer.Stop();
            times.Add(timer.Elapsed);
        }

        var fastest = times.Min();
        output.WriteLine($"coverage: {behaviours.Count} behaviours × {tests.Count} tests in {fastest.TotalMilliseconds:F0} ms "
            + $"(runs: {string.Join(", ", times.Select(t => $"{t.TotalMilliseconds:F0}"))} ms); {matches.Count(m => m is not null)} covered");
        Assert.True(matches.Count(m => m is not null) >= 10);
        Assert.True(fastest < TimeSpan.FromSeconds(2), $"coverage took {fastest}");
    }
}
