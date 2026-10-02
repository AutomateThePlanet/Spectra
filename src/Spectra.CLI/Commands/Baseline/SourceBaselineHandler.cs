using Spectra.CLI.Baseline;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>Spec 084 (FR-026, FR-026a, FR-028): the logic of <c>spectra ai source-baseline --init</c>.</summary>
public static class SourceBaselineHandler
{
    public const string Command = "source-baseline";

    private sealed record SuiteResult(string Suite, BaselineWriteSummary Summary);

    public static async Task<int> RunAsync(
        string root, bool init, string? suite, bool allSuites, bool force, bool dryRun, bool json, CancellationToken ct)
    {
        if (!init)
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments,
                "source-baseline needs --init (the only mode in this version).");
        }

        if (allSuites == !string.IsNullOrWhiteSpace(suite))
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments,
                "Give exactly one of --suite <name> and --all-suites.");
        }

        var loaded = await BaselineCommandIO.LoadConfigAsync(root, ct);
        if (!loaded.IsSuccess)
        {
            return BaselineCommandIO.Fail(json, Command, loaded.ErrorCode!, loaded.Error!);
        }

        var config = loaded.Config!;
        var warnings = new List<string>(loaded.Warnings);
        BaselineCommandIO.PrintWarnings(loaded.Warnings);
        var testsPath = BaselineCommandIO.TestsPath(root, config);

        var (suites, scopeError, scopeMessage) = BaselineCommandIO.ResolveScope(testsPath, suite, allSuites);
        if (suites is null)
        {
            return BaselineCommandIO.Fail(json, Command, scopeError!, scopeMessage!, warnings);
        }

        var results = new List<SuiteResult>();
        var total = new BaselineWriteSummary();
        IReadOnlyCollection<string> notes = [];
        var counted = new HashSet<string>(StringComparer.Ordinal);

        async Task<int> Body()
        {
            var context = await BaselineContext.CreateAsync(root, config, ct);
            foreach (var name in suites)
            {
                var tests = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, name, warnings, ct);
                var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, name, ct);
                var summary = new BaselineWriteSummary();
                var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var test in tests.Tests)
                {
                    var computed = await BaselineRecorder.ComputeAsync(test, context, ct);
                    var needed = force
                        ? BaselineRecorder.Replace(baseline, test.Id, computed, summary)
                        : BaselineRecorder.Complete(baseline, test.Id, computed, summary);
                    foreach (var (fingerprint, text) in needed) snapshots[fingerprint] = text;
                }

                if (force)
                {
                    summary.Recomputed = summary.Updated;
                }

                if (dryRun)
                {
                    BaselineRecorder.CountMissingSnapshots(root, snapshots.Where(s => counted.Add(s.Key)), total);
                }
                else
                {
                    // Snapshots first: a crash in between leaves a ref whose snapshot exists, never the reverse.
                    await BaselineRecorder.WriteSnapshotsAsync(root, snapshots, total, ct);
                    var live = tests.Tests.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
                    await BaselineStore.SaveSuiteAsync(testsPath, name, baseline, live, ct);
                }

                results.Add(new SuiteResult(name, summary));
            }

            notes = context.Notes;
            return 0;
        }

        try
        {
            if (dryRun) await Body();
            else await BaselineStore.WithLockAsync(root, Body, ct);
        }
        catch (BaselineStoreException ex)
        {
            return BaselineCommandIO.Fail(json, Command, ex.ErrorCode, ex.Message, warnings);
        }

        var document = new Dictionary<string, object?>
        {
            ["command"] = Command,
            ["success"] = true,
            ["scope"] = BaselineCommandIO.ScopeJson(allSuites ? null : suite),
            ["dry_run"] = dryRun,
            ["force"] = force,
            ["suites"] = results.Select(r => new Dictionary<string, object?>
            {
                ["suite"] = r.Suite,
                ["created"] = r.Summary.Created,
                ["completed"] = r.Summary.Completed,
                ["unchanged"] = r.Summary.Unchanged,
                ["recomputed"] = r.Summary.Recomputed
            }).ToList(),
            ["snapshots_written"] = total.SnapshotsWritten,
            ["notes"] = notes.ToList(),
            ["warnings"] = warnings
        };

        BaselineCommandIO.Emit(json, document, writer =>
        {
            var verb = dryRun ? "would be" : "were";
            foreach (var r in results)
            {
                writer.WriteLine($"Source baseline — {r.Suite}: {r.Summary.Created} created, {r.Summary.Completed} completed, "
                    + $"{r.Summary.Unchanged} unchanged{(force ? $", {r.Summary.Recomputed} recomputed" : "")}");
            }

            writer.WriteLine($"Snapshots: {total.SnapshotsWritten} {verb} written.{(dryRun ? " Dry run: nothing written." : "")}");
            foreach (var note in notes) writer.WriteLine($"  note: {note}");
        });

        return ExitCodes.Success;
    }
}
