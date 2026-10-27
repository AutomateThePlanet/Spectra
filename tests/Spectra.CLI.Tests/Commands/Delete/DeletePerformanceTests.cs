using System.Diagnostics;
using Spectra.CLI.Commands.Delete;
using Spectra.CLI.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Spectra.CLI.Tests.Commands.Delete;

/// <summary>
/// Spec 096 F6 (FR-010; analyze H2): deleting tests no longer fully parses every test file. Dependents are found from
/// frontmatter; only the targets and their scalar dependents are parsed in full — a deterministic count, not a timing.
/// The time is only logged (SC-005 is a recorded measurement, 23aa08c vs 096). The parity row pins the plan's results
/// (a guard: same on 23aa08c). On 23aa08c every file was parsed in full and the counter didn't exist.
/// Spec 099 F7: not in a serial collection — it calls <c>TestDeletionService</c> on an explicit root (no current
/// directory, no console, no static override), so it runs in parallel with the other classes.
/// </summary>
public sealed class DeletePerformanceTests(ITestOutputHelper output) : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private string TestCases => _ws.PathOf("test-cases");

    private void Write(string suite, string id, string extraFrontmatter = "", string? content = null)
    {
        var path = _ws.PathOf($"test-cases/{suite}/{id}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content ?? BaselineTestWorkspace.TestMarkdown(id, ["docs/a.md"], title: $"Behaviour {id}")
            .Replace($"id: {id}\n", $"id: {id}\n{extraFrontmatter}"));
    }

    [Fact]
    public async Task DeletingOneOf2500_FullyParsesOnlyTheTargetAndItsScalarDependent()
    {
        for (var s = 0; s < 5; s++)
        {
            for (var i = 0; i < 500; i++)
            {
                var n = s * 500 + i + 1;
                Write($"suite{s}", $"TC-{n:D4}", n == 1250 ? "depends_on: TC-0001\n" : "");
            }

            _ws.RebuildIndex($"suite{s}");
        }

        var timer = Stopwatch.StartNew();
        var service = await TestDeletionService.LoadAsync(_ws.Root, TestCases);
        var load = timer.Elapsed;
        var targets = service.Resolve(["TC-0001"]).Select(r => r.Target!).ToList();
        var resolve = timer.Elapsed - load;
        var plan = await service.PlanAsync(targets);
        timer.Stop();
        output.WriteLine($"2,500 tests: load {load.TotalMilliseconds:F0} ms, resolve {resolve.TotalMilliseconds:F0} ms, "
                         + $"total {timer.Elapsed.TotalMilliseconds:F0} ms; full parses: {service.FullParses}");

        Assert.Equal(["TC-1250"], plan.ScalarDependents.Select(d => d.DependentId));
        Assert.Equal(2, service.FullParses);
    }

    [Fact]
    public async Task ThePlan_IsTheSame_ForScalarListAndUnparseableDependents_AcrossSuites()
    {
        Write("s1", "TC-1");
        Write("s2", "TC-2");
        Write("s1", "TC-3", "depends_on: TC-1\n");
        Write("s2", "TC-4", "depends_on:\n  - TC-2\n");
        Write("s3", "TC-5", content: "---\nid: TC-5\ndepends_on: TC-1\npriority:\n  - high\n---\n\n# Wrong types\n");
        Write("s3", "TC-6", "depends_on: TC-9\n");
        _ws.RebuildIndex("s1"); // s2 and s3 have no _index.json

        var service = await TestDeletionService.LoadAsync(_ws.Root, TestCases);
        var resolved = service.Resolve(["TC-1", "TC-2", "TC-404"]);
        var plan = await service.PlanAsync(resolved.Where(r => r.Target is not null).Select(r => r.Target!).ToList());

        Assert.Null(resolved.Single(r => r.Id == "TC-404").Target);
        Assert.Equal(["s1", "s2"], resolved.Where(r => r.Target is not null).Select(r => r.Target!.Suite));
        Assert.Equal(["TC-3"], plan.ScalarDependents.Select(d => d.DependentId));
        Assert.Equal(2, plan.Warnings.Count);
        Assert.Contains(plan.Warnings, w => w.StartsWith("TC-4: depends_on is a list", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, w => w.Contains("doesn't parse", StringComparison.Ordinal) && w.Contains("TC-1"));
    }
}
