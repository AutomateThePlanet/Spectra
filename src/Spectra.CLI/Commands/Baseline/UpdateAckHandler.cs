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
/// <c>CRITERION_REMOVED</c>): it is cleared by fixing or dropping the ref, not by an ack.
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

            await RefuseRemovedAsync(targets, baseline, context, ct);

            var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var test in targets.OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                var computed = await BaselineRecorder.ComputeAsync(test, context, ct);
                foreach (var (fingerprint, text) in BaselineRecorder.Replace(baseline, test.Id, computed, summary))
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
                await BaselineStore.SaveSuiteAsync(testsPath, suite!, baseline,
                    tests.Tests.Select(t => t.Id).ToHashSet(StringComparer.Ordinal), ct);
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
        var path = Path.Combine(root, ".spectra", "update-triage.json");
        if (!File.Exists(path))
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"No triage to resolve group {group} from: run 'spectra ai update-triage --suite {suite}' first.");
        }

        JsonElement detail;
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
            detail = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"{UpdateTriageHandler.DetailFile} is not a triage result: run 'spectra ai update-triage --suite {suite}' again.");
        }

        if (detail.ValueKind != JsonValueKind.Object
            || !detail.TryGetProperty("groups", out var groupsElement) || groupsElement.ValueKind != JsonValueKind.Array
            || !detail.TryGetProperty("tests", out var testsElement) || testsElement.ValueKind != JsonValueKind.Array)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"{UpdateTriageHandler.DetailFile} is not a triage result: run 'spectra ai update-triage --suite {suite}' again.");
        }

        var scope = detail.TryGetProperty("scope", out var s) ? s : default;
        var covers = scope.ValueKind == JsonValueKind.Object
            && ((scope.TryGetProperty("all_suites", out var all) && all.ValueKind == JsonValueKind.True)
                || (scope.TryGetProperty("suite", out var only) && only.GetString() == suite));
        if (!covers)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"The last triage did not cover suite '{suite}': run 'spectra ai update-triage --suite {suite}' first.");
        }

        var match = groupsElement.EnumerateArray()
            .FirstOrDefault(g => string.Equals(g.GetProperty("id").GetString(), group, StringComparison.OrdinalIgnoreCase));
        if (match.ValueKind != JsonValueKind.Object)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorGroupNotFound, $"Group {group} is not in the last triage.");
        }

        var groupSuite = match.GetProperty("suite").GetString();
        if (groupSuite != suite)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorGroupNotFound,
                $"Group {group} is in suite '{groupSuite}', not '{suite}'.");
        }

        var bucket = match.GetProperty("bucket").GetString();
        if (bucket is "REMOVED" or "CRITERIA_REMOVED")
        {
            throw new BaselineStoreException(
                bucket == "REMOVED" ? BaselineCommandIO.ErrorRefRemoved : BaselineCommandIO.ErrorCriterionRemoved,
                $"Group {group} is {bucket}: fix or drop the removed ref/criterion in each test, then ack with --ids.");
        }

        var recorded = testsElement.EnumerateArray()
            .Where(t => t.GetProperty("suite").GetString() == suite)
            .ToDictionary(t => t.GetProperty("id").GetString()!, t => t.GetProperty("current"), StringComparer.Ordinal);

        var targets = new List<TestCase>();
        var stale = new List<string>();
        foreach (var id in match.GetProperty("tests").EnumerateArray().Select(t => t.GetString()!))
        {
            if (!byId.TryGetValue(id, out var test) || !recorded.TryGetValue(id, out var current)
                || !await MatchesAsync(test, current, context, ct))
            {
                stale.Add(id);
                continue;
            }

            targets.Add(test);
        }

        if (stale.Count > 0)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageStale,
                $"Group {group} changed since the last triage ({string.Join(", ", stale)}): run 'spectra ai update-triage --suite {suite}' again and review.");
        }

        return targets;
    }

    /// <summary>Whether the test's current refs and criteria (and their fingerprints) are what triage recorded.</summary>
    private static async Task<bool> MatchesAsync(TestCase test, JsonElement recorded, BaselineContext context, CancellationToken ct)
    {
        var refs = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            refs[sourceRef] = (await context.GetSectionAsync(sourceRef, ct)).Current.Fingerprint;
        }

        var criteria = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var id in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
        {
            criteria[id] = context.GetCriterion(id).Fingerprint;
        }

        return SameMap(refs, recorded.GetProperty("refs")) && SameMap(criteria, recorded.GetProperty("criteria"));
    }

    private static bool SameMap(SortedDictionary<string, string?> now, JsonElement recorded)
    {
        var then = recorded.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString());
        return then.Count == now.Count && now.All(kv => then.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }

    private static async Task RefuseRemovedAsync(
        IEnumerable<TestCase> targets, SourceBaseline baseline, BaselineContext context, CancellationToken ct)
    {
        var removedRefs = new List<string>();
        var removedCriteria = new List<string>();
        foreach (var test in targets)
        {
            if (!baseline.Tests.TryGetValue(test.Id, out var entry))
            {
                continue; // unbaselined: nothing is REMOVED, the ack creates the entry
            }

            foreach (var sourceRef in test.SourceRefs.Where(entry.Refs.ContainsKey).Distinct(StringComparer.Ordinal))
            {
                if ((await context.GetSectionAsync(sourceRef, ct)).Current.Missing)
                {
                    removedRefs.Add($"{test.Id}: {sourceRef}");
                }
            }

            foreach (var id in test.Criteria.Where(entry.Criteria.ContainsKey).Distinct(StringComparer.Ordinal))
            {
                if (!context.GetCriterion(id).Exists)
                {
                    removedCriteria.Add($"{test.Id}: {id}");
                }
            }
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
