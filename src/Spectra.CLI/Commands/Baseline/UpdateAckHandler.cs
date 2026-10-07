using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Baseline;
using Spectra.Core.Models;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 084: the logic of <c>spectra ai update-ack</c>. <c>--group</c> resolves through the last triage's detail file
/// and is refused when the group's sections or criteria changed since (<c>TRIAGE_STALE</c>), so an ack never swallows a
/// change triage didn't show. A test that still lists a REMOVED ref or criterion is refused (<c>REF_REMOVED</c> /
/// <c>CRITERION_REMOVED</c>): it is cleared by fixing or dropping the ref, not by an ack. Spec 086 (FR-014): so is a
/// test with a RELINK ref — a missing file that git history renamed, baselined or not — with a message naming
/// <c>relink-sources</c>; an ack never creates an entry for such a test.
/// </summary>
public static class UpdateAckHandler
{
    public const string Command = "update-ack";

    public static async Task<int> RunAsync(
        string root, string? suite, string? ids, string? group, bool dryRun, bool json, CancellationToken ct)
    {
        var idList = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(suite) || (idList.Length > 0) == !string.IsNullOrWhiteSpace(group))
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments,
                "Give --suite <name> and exactly one of --ids a,b and --group <id>.");
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

        var (suites, scopeError, scopeMessage) = BaselineCommandIO.ResolveScope(testsPath, suite, allSuites: false);
        if (suites is null)
        {
            return BaselineCommandIO.Fail(json, Command, scopeError!, scopeMessage!, warnings);
        }

        var summary = new BaselineWriteSummary();
        List<string> acked = [];

        async Task<int> Body()
        {
            var tests = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, suite!, warnings, ct);
            var byId = tests.Tests.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
            var context = await BaselineContext.CreateAsync(root, config, ct);
            var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, suite!, ct);

            List<TestCase> targets;
            if (group is null)
            {
                var missing = idList.Where(id => !byId.ContainsKey(id)).ToList();
                if (missing.Count > 0)
                {
                    throw new BaselineStoreException(BaselineCommandIO.ErrorTestNotFound,
                        $"Not a test in suite '{suite}': {string.Join(", ", missing)}.");
                }

                targets = idList.Select(id => byId[id]).DistinctBy(t => t.Id).ToList();
            }
            else
            {
                targets = await ResolveGroupAsync(root, suite!, group, byId, context, ct);
            }

            await RefuseRemovedAsync(targets, baseline, context, suite!, ct);

            var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var test in targets.OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                var computed = await BaselineRecorder.ComputeAsync(test, context, ct);
                foreach (var (fingerprint, text) in BaselineRecorder.Replace(baseline, test, computed, summary))
                {
                    snapshots[fingerprint] = text;
                }

                acked.Add(test.Id);
            }

            if (dryRun)
            {
                BaselineRecorder.CountMissingSnapshots(root, snapshots, summary);
            }
            else
            {
                await BaselineRecorder.WriteSnapshotsAsync(root, snapshots, summary, ct);
                await BaselineStore.SaveSuiteAsync(testsPath, suite!, baseline, tests.Scan, warnings, ct);
            }

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

        BaselineCommandIO.PrintLateWarnings(warnings, loaded.Warnings.Count);

        var document = new Dictionary<string, object?>
        {
            ["command"] = Command,
            ["success"] = true,
            ["suite"] = suite,
            ["dry_run"] = dryRun,
            ["tests"] = acked,
            ["baseline"] = summary.ToJson(),
            ["warnings"] = warnings
        };

        BaselineCommandIO.Emit(json, document, writer =>
        {
            writer.WriteLine($"Acknowledged {acked.Count} test(s) in '{suite}': {string.Join(", ", acked)}"
                + (dryRun ? " (dry run: nothing written)" : ""));
            writer.WriteLine("  " + summary.ToHuman());
        });

        return ExitCodes.Success;
    }

    private static async Task<List<TestCase>> ResolveGroupAsync(
        string root, string suite, string group, Dictionary<string, TestCase> byId, BaselineContext context, CancellationToken ct)
    {
        var (_, tests) = await TriageGroupResolver.ResolveAsync(root, suite, group, byId, context, bucket =>
        {
            if (bucket == "RELINK")
            {
                // Spec 086 FR-014 (analysis C5).
                throw new BaselineStoreException(BaselineCommandIO.ErrorRefRemoved,
                    $"Group {group} is RELINK: its doc files were renamed. {RelinkHint(suite)}");
            }

            if (bucket is "REMOVED" or "CRITERIA_REMOVED")
            {
                throw new BaselineStoreException(
                    bucket == "REMOVED" ? BaselineCommandIO.ErrorRefRemoved : BaselineCommandIO.ErrorCriterionRemoved,
                    $"Group {group} is {bucket}: fix or drop the removed ref/criterion in each test, then ack with --ids.");
            }
        }, ct);
        return tests;
    }

    private static string RelinkHint(string suite) =>
        $"Run 'spectra ai relink-sources --suite {suite} --dry-run' to review, then without --dry-run, then triage again.";

    private static async Task RefuseRemovedAsync(
        IEnumerable<TestCase> targets, SourceBaseline baseline, BaselineContext context, string suite, CancellationToken ct)
    {
        var relinkRefs = new List<string>();
        var removedRefs = new List<string>();
        var removedCriteria = new List<string>();
        foreach (var test in targets)
        {
            baseline.Tests.TryGetValue(test.Id, out var entry);
            foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
            {
                if (!(await context.GetSectionAsync(sourceRef, ct)).Current.Missing)
                {
                    continue;
                }

                // Spec 086 FR-014 (analysis U1): every missing file is asked about, with or without a baseline key, so
                // a test with a RELINK ref never gets an entry from an ack.
                if (await context.RelinkCandidateAsync(sourceRef, context.Renames, ct) is { NewRef: not null } candidate)
                {
                    relinkRefs.Add($"{test.Id}: {sourceRef} → {candidate.NewRef}");
                }
                else if (entry is not null && entry.Refs.ContainsKey(sourceRef))
                {
                    removedRefs.Add($"{test.Id}: {sourceRef}");
                }
            }

            if (entry is null)
            {
                continue; // unbaselined: no criterion is REMOVED, the ack creates the entry
            }

            foreach (var id in test.Criteria.Where(entry.Criteria.ContainsKey).Distinct(StringComparer.Ordinal))
            {
                if (!context.GetCriterion(id).Exists)
                {
                    removedCriteria.Add($"{test.Id}: {id}");
                }
            }
        }

        if (relinkRefs.Count > 0)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorRefRemoved,
                $"Can't acknowledge a renamed doc ref ({string.Join("; ", relinkRefs)}). {RelinkHint(suite)}"
                + (removedRefs.Count > 0 ? $" Also REMOVED: {string.Join("; ", removedRefs)}." : ""));
        }

        if (removedRefs.Count > 0)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorRefRemoved,
                $"Can't acknowledge a REMOVED ref ({string.Join("; ", removedRefs)}). Fix or drop the ref in the test's "
                + "source_refs, then run update-ack again.");
        }

        if (removedCriteria.Count > 0)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorCriterionRemoved,
                $"Can't acknowledge a REMOVED criterion ({string.Join("; ", removedCriteria)}). Fix or drop the ID in the "
                + "test's criteria, then run update-ack again.");
        }
    }
}
