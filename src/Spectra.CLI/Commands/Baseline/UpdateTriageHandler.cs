using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Baseline;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>Spec 084: the logic of <c>spectra ai update-triage</c> — run, auto-ack, detail file, outputs.</summary>
public static class UpdateTriageHandler
{
    public const string Command = "update-triage";

    public const string DetailFile = ".spectra/update-triage.json";

    public static async Task<int> RunAsync(
        string root, string? suite, bool allSuites, bool noAutoAck, bool dryRun, bool json, CancellationToken ct)
    {
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

        var autoAck = !noAutoAck;
        var writesBaseline = autoAck && !dryRun;
        TriageRun? run = null;
        var autoAcked = 0;

        async Task<int> Body()
        {
            run = await TriageEngine.RunAsync(root, config, testsPath, suites, autoAck, dryRun, warnings, ct);
            if (writesBaseline && run.AutoAcks.Count > 0)
            {
                autoAcked = await ApplyAutoAcksAsync(root, testsPath, run, warnings, ct);
            }

            return 0;
        }

        try
        {
            if (writesBaseline) await BaselineStore.WithLockAsync(root, Body, ct);
            else await Body();
        }
        catch (BaselineStoreException ex)
        {
            return BaselineCommandIO.Fail(json, Command, ex.ErrorCode, ex.Message, warnings);
        }

        if (!dryRun)
        {
            await BaselineStore.WriteAtomicAsync(Path.Combine(root, ".spectra", "update-triage.json"),
                JsonSerializer.Serialize(DetailDocument(run!, allSuites ? null : suite), BaselineCommandIO.Json), ct);
        }

        BaselineCommandIO.PrintLateWarnings(warnings, loaded.Warnings.Count);
        BaselineCommandIO.Emit(json, StdoutDocument(run!, allSuites ? null : suite, dryRun, autoAck, autoAcked, warnings),
            writer => TriagePresenter.Write(writer, run!, autoAck, dryRun, autoAcked));
        return ExitCodes.Success;
    }

    /// <summary>Refreshes only the COSMETIC refs' fingerprints (every other key is kept, FR-018 / FR-026a).</summary>
    private static async Task<int> ApplyAutoAcksAsync(
        string root, string testsPath, TriageRun run, List<string> warnings, CancellationToken ct)
    {
        var applied = 0;
        foreach (var bySuite in run.AutoAcks.GroupBy(a => a.Suite))
        {
            var suiteRun = run.Suites.Single(s => s.Suite == bySuite.Key);
            var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, bySuite.Key, ct);
            foreach (var ack in bySuite)
            {
                await BaselineStore.EnsureSnapshotAsync(root, ack.Fingerprint, ack.Normalised, ct);
                if (baseline.Tests.TryGetValue(ack.Id, out var entry))
                {
                    entry.Refs[ack.Ref] = ack.Fingerprint;
                    applied++;
                }
            }

            await BaselineStore.SaveSuiteAsync(testsPath, bySuite.Key, baseline, suiteRun.Scan, warnings, ct);
        }

        return applied;
    }

    private static Dictionary<string, object?> StdoutDocument(
        TriageRun run, string? suite, bool dryRun, bool autoAck, int autoAcked, List<string> warnings)
    {
        var document = new Dictionary<string, object?>
        {
            ["command"] = Command,
            ["success"] = true,
            ["scope"] = BaselineCommandIO.ScopeJson(suite),
            ["dry_run"] = dryRun,
            ["suites"] = run.Suites.Select(s => new Dictionary<string, object?>
            {
                ["suite"] = s.Suite,
                ["tests"] = s.TestCount,
                ["counts"] = Counts(s)
            }).ToList(),
            ["groups"] = run.Groups.Select(GroupJson).ToList(),
            ["removed"] = Removed(run),
            ["unbaselined"] = run.Suites
                .Where(s => s.UnbaselinedIds.Count > 0 || s.PartialIds.Count > 0)
                .Select(s => new Dictionary<string, object?> { ["suite"] = s.Suite, ["full"] = s.UnbaselinedIds.Count, ["partial"] = s.PartialIds.Count })
                .ToList(),
            ["no_tests"] = run.NoTests.Select(n => new Dictionary<string, object?> { ["doc"] = n.Doc, ["heading"] = n.Heading }).ToList(),
            ["notes"] = run.Notes.ToList()
        };

        if (dryRun)
        {
            document["would_auto_ack"] = autoAck ? run.AutoAcks.Count : 0;
        }
        else
        {
            document["auto_acked"] = autoAcked;
        }

        document["detail_file"] = dryRun ? null : DetailFile;
        document["warnings"] = warnings;
        return document;
    }

    /// <summary>
    /// A group in the compact stdout: <c>refs</c> / <c>criteria</c> appear only when the group has some (SC-004 budget).
    /// </summary>
    private static Dictionary<string, object?> GroupJson(TriageGroup group)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = group.Id,
            ["suite"] = group.Suite,
            ["bucket"] = group.Bucket.Wire()
        };
        var refs = group.Items.Where(i => !i.IsCriterion).Select(RefItemJson).ToList();
        var criteria = group.Items.Where(i => i.IsCriterion).Select(i => new Dictionary<string, object?> { ["id"] = i.Key, ["state"] = i.State }).ToList();
        if (refs.Count > 0) json["refs"] = refs;
        if (criteria.Count > 0) json["criteria"] = criteria;
        json["tests"] = group.Tests;
        json["why"] = group.Why;
        return json;
    }

    /// <summary>A group's ref item: <c>ref</c>, <c>state</c>, and (Spec 086, RELINK only) the <c>target</c>.</summary>
    private static Dictionary<string, object?> RefItemJson(ChangedItem item)
    {
        var json = new Dictionary<string, object?> { ["ref"] = item.Key, ["state"] = item.State };
        if (item.Target is not null) json["target"] = item.Target;
        if (item.Submodule) json["submodule"] = true;
        return json;
    }

    /// <summary>The non-zero counts per bucket (a missing bucket is 0), worst first, then unbaselined and partial.</summary>
    private static Dictionary<string, object?> Counts(TriagedSuite suite)
    {
        var counts = new Dictionary<string, object?>();
        foreach (var bucket in BucketRules.WorstFirst)
        {
            var n = suite.Tests.Count(t => t.Bucket == bucket);
            if (n > 0) counts[bucket.Wire().ToLowerInvariant()] = n;
        }

        if (suite.UnbaselinedIds.Count > 0) counts["unbaselined"] = suite.UnbaselinedIds.Count;
        if (suite.PartialIds.Count > 0) counts["partial"] = suite.PartialIds.Count;
        return counts;
    }

    private static List<Dictionary<string, object?>> Removed(TriageRun run)
    {
        var removed = new List<Dictionary<string, object?>>();
        foreach (var test in run.Suites.SelectMany(s => s.Tests))
        {
            foreach (var r in test.Refs.Where(r => r.Verdict.State == RefState.Removed))
            {
                removed.Add(new Dictionary<string, object?>
                {
                    ["suite"] = test.Suite, ["test"] = test.Id, ["ref"] = r.Verdict.Ref, ["reason"] = r.Verdict.Why.FirstOrDefault()
                });
            }

            foreach (var c in test.Criteria.Where(c => c.State == CriterionState.Removed))
            {
                removed.Add(new Dictionary<string, object?>
                {
                    ["suite"] = test.Suite, ["test"] = test.Id, ["criterion"] = c.Id, ["reason"] = "not in any criteria file"
                });
            }
        }

        return removed;
    }

    /// <summary>The detail document (data-model): every test's states, fingerprints and diffs; group membership.</summary>
    internal static Dictionary<string, object?> DetailDocument(TriageRun run, string? suite) => new()
    {
        ["version"] = 1,
        ["scope"] = BaselineCommandIO.ScopeJson(suite),
        ["config"] = new Dictionary<string, object?> { ["group_size"] = run.GroupSize, ["light_threshold_words"] = run.LightThreshold },
        ["tests"] = run.Suites.SelectMany(s => s.Tests).Select(TestDetail).ToList(),
        ["groups"] = run.Groups.Select(g => new Dictionary<string, object?>
        {
            ["id"] = g.Id, ["suite"] = g.Suite, ["bucket"] = g.Bucket.Wire(), ["tests"] = g.Tests,
            ["items"] = g.Items.Select(i => i.Signature).ToList()
        }).ToList(),
        ["unbaselined"] = run.Suites.ToDictionary(s => s.Suite, s => (object?)new Dictionary<string, object?>
        {
            ["full"] = s.UnbaselinedIds, ["partial"] = s.PartialIds
        }),
        ["notes"] = run.Notes.ToList()
    };

    /// <summary>One test's detail record. Spec 086 (analysis C6): a RELINK test with no entry adds <c>unbaselined: true</c>.</summary>
    private static Dictionary<string, object?> TestDetail(TriagedTest t)
    {
        var detail = new Dictionary<string, object?>
        {
            ["suite"] = t.Suite,
            ["id"] = t.Id,
            ["bucket"] = t.Bucket.Wire(),
            ["partial"] = t.Partial && !t.NoEntry,
            ["refs"] = t.Refs.Select(r => RefDetail(r.Verdict, r.Target)).ToList(),
            ["criteria"] = t.Criteria.Select(c => new Dictionary<string, object?>
            {
                ["id"] = c.Id, ["state"] = c.State.Wire(), ["baseline"] = c.Baseline, ["current"] = c.Current
            }).ToList(),
            ["unbaselined_refs"] = t.UnbaselinedRefs,
            ["unbaselined_criteria"] = t.UnbaselinedCriteria,
            ["current"] = new Dictionary<string, object?> { ["refs"] = t.CurrentRefs, ["criteria"] = t.CurrentCriteria }
        };
        if (t.NoEntry) detail["unbaselined"] = true;
        return detail;
    }

    private static Dictionary<string, object?> RefDetail(RefVerdict verdict, string? target)
    {
        var detail = new Dictionary<string, object?>
        {
            ["ref"] = verdict.Ref,
            ["state"] = verdict.State.Wire(),
            ["baseline"] = verdict.Baseline,
            ["current"] = verdict.Current
        };
        if (target is not null) detail["target"] = target;
        if (verdict.Why.Count > 0) detail["why"] = verdict.Why;
        if (verdict.Diff.Count > 0)
        {
            detail["diff"] = verdict.Diff.Select(d => new Dictionary<string, object?> { [d.Added ? "added" : "removed"] = d.Words }).ToList();
        }

        return detail;
    }
}
