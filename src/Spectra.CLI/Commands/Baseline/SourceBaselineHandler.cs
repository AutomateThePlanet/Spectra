using Spectra.CLI.Baseline;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Baseline;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 084 (FR-026, FR-026a, FR-028): the logic of <c>spectra ai source-baseline --init</c>. Spec 086 (FR-015–FR-018):
/// <c>--from-date</c> / <c>--from-ref --owner</c> fill missing entries from the docs as they were at that point
/// (history bootstrap); existing fingerprints are never changed.
/// </summary>
public static class SourceBaselineHandler
{
    public const string Command = "source-baseline";

    private sealed record SuiteResult(string Suite, BaselineWriteSummary Summary, IReadOnlyList<UnbaselinedRef> Unbaselined);

    private sealed record UnbaselinedRef(string Test, string Ref, string Reason);

    public static Task<int> RunAsync(
        string root, bool init, string? suite, bool allSuites, bool force, bool dryRun, bool json, CancellationToken ct) =>
        RunAsync(root, init, suite, allSuites, force, dryRun, fromDate: null, fromRef: null, owner: null, minSimilarity: null, json, ct);

    public static async Task<int> RunAsync(
        string root, bool init, string? suite, bool allSuites, bool force, bool dryRun, string? fromDate, string? fromRef,
        string? owner, int? minSimilarity, bool json, CancellationToken ct)
    {
        // Spec 086 FR-018: the history-bootstrap refusals, before anything is read.
        var refusal = (fromDate, fromRef, owner) switch
        {
            _ when (fromDate is not null || fromRef is not null || owner is not null) && !init =>
                "--from-date, --from-ref and --owner need --init.",
            _ when force && (fromDate is not null || fromRef is not null) =>
                "--force can't be combined with --from-date or --from-ref: history bootstrap only fills missing entries.",
            (not null, not null, _) => "Give at most one of --from-date and --from-ref.",
            (_, not null, null) => "--from-ref needs --owner <path> (the repository it names a commit in).",
            (_, null, not null) => "--owner is only used with --from-ref.",
            _ => null
        };
        // Spec 087 (F5, FR-015): --min-similarity only applies to the history scan, validated like relink-sources.
        refusal ??= minSimilarity switch
        {
            { } when fromDate is null && fromRef is null => "--min-similarity is only used with --from-date or --from-ref.",
            { } value => BaselineCommandIO.ValidateMinSimilarity(value),
            null => null
        };
        if (refusal is not null)
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments, refusal);
        }

        var threshold = minSimilarity ?? RenameResolver.DefaultMinSimilarity;

        DateTimeOffset date = default;
        if (fromDate is not null && !HistoryBootstrap.TryParseDate(fromDate, out date))
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments,
                $"--from-date '{fromDate}' is not an ISO 8601 date (e.g. 2026-03-01).");
        }

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
        HistoryBootstrap? bootstrap = null;

        async Task<int> Body()
        {
            var context = await BaselineContext.CreateAsync(root, config, ct);
            if (fromDate is not null)
            {
                bootstrap = HistoryBootstrap.FromDate(context, context.Refs, date, threshold);
            }
            else if (fromRef is not null)
            {
                bootstrap = await HistoryBootstrap.FromRefAsync(context, context.Refs, owner!, fromRef, threshold, ct);
            }

            foreach (var name in suites)
            {
                var tests = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, name, warnings, ct);
                var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, name, ct);
                var summary = new BaselineWriteSummary();
                var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
                var unbaselined = new List<UnbaselinedRef>();

                if (bootstrap is not null)
                {
                    await bootstrap.PrepareAsync(tests.Tests.SelectMany(t => MissingRefs(t, baseline)), ct);
                }

                foreach (var test in tests.Tests)
                {
                    var computed = await BaselineRecorder.ComputeAsync(test, context, ct);
                    if (bootstrap is not null)
                    {
                        computed = Historic(test, baseline, computed, bootstrap, unbaselined);
                    }

                    var needed = force
                        ? BaselineRecorder.Replace(baseline, test, computed, summary)
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
                    await BaselineStore.SaveSuiteAsync(testsPath, name, baseline, tests.Scan, warnings, ct);
                }

                results.Add(new SuiteResult(name, summary, unbaselined));
            }

            notes = context.Notes;

            // Spec 087 (F1, FR-004a): refs into a submodule that isn't checked out stay unbaselined; say so once per submodule.
            var scoped = new List<Spectra.Core.Models.TestCase>();
            foreach (var name in suites)
            {
                scoped.AddRange((await BaselineStore.ScanSuiteAsync(root, testsPath, name, ct)).Tests);
            }

            warnings.AddRange(await context.SubmoduleNotesAsync(scoped, ct));
            if (bootstrap is { CurrentContentDocs.Count: > 0 })
            {
                warnings.Add((fromRef is not null
                        ? $"Only the repository of --owner {owner} is pinned to {fromRef}; these docs used current content: "
                        : "These docs are in no git repository (or one without history) and used current content: ")
                    + string.Join(", ", bootstrap.CurrentContentDocs) + ".");
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
            ["scope"] = BaselineCommandIO.ScopeJson(allSuites ? null : suite),
            ["dry_run"] = dryRun,
            ["force"] = force,
            ["suites"] = results.Select(r =>
            {
                var item = new Dictionary<string, object?>
                {
                    ["suite"] = r.Suite,
                    ["created"] = r.Summary.Created,
                    ["completed"] = r.Summary.Completed,
                    ["unchanged"] = r.Summary.Unchanged,
                    ["recomputed"] = r.Summary.Recomputed
                };
                if (bootstrap is not null)
                {
                    item["unbaselined"] = r.Unbaselined.Select(u => new Dictionary<string, object?>
                    {
                        ["test"] = u.Test, ["ref"] = u.Ref, ["reason"] = u.Reason
                    }).ToList();
                }

                return item;
            }).ToList(),
            ["snapshots_written"] = total.SnapshotsWritten,
            ["notes"] = notes.ToList(),
            ["warnings"] = warnings
        };
        if (fromDate is not null || fromRef is not null)
        {
            // Spec 086: only with a history bootstrap, so a plain --init's output is unchanged.
            document["from"] = fromDate is not null
                ? new Dictionary<string, object?> { ["date"] = fromDate, ["min_similarity"] = threshold }
                : new Dictionary<string, object?> { ["ref"] = fromRef, ["owner"] = owner, ["min_similarity"] = threshold };
        }

        BaselineCommandIO.Emit(json, document, writer =>
        {
            var verb = dryRun ? "would be" : "were";
            if (fromDate is not null || fromRef is not null)
            {
                writer.WriteLine(fromDate is not null
                    ? $"History bootstrap: docs as of {fromDate} (each repository's last commit at or before it)"
                    : $"History bootstrap: {owner} at {fromRef}");
            }

            foreach (var r in results)
            {
                writer.WriteLine($"Source baseline — {r.Suite}: {r.Summary.Created} created, {r.Summary.Completed} completed, "
                    + $"{r.Summary.Unchanged} unchanged{(force ? $", {r.Summary.Recomputed} recomputed" : "")}");
                foreach (var u in r.Unbaselined)
                {
                    writer.WriteLine($"  unbaselined: {u.Test} {u.Ref} — {u.Reason}");
                }
            }

            writer.WriteLine($"Snapshots: {total.SnapshotsWritten} {verb} written.{(dryRun ? " Dry run: nothing written." : "")}");
            foreach (var note in notes) writer.WriteLine($"  note: {note}");
        });

        return ExitCodes.Success;
    }

    /// <summary>The refs of <paramref name="test"/> that its entry lacks (all of them when it has none).</summary>
    private static IEnumerable<string> MissingRefs(Spectra.Core.Models.TestCase test, SourceBaseline baseline)
    {
        baseline.Tests.TryGetValue(test.Id, out var entry);
        return test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r) && entry?.Refs.ContainsKey(r) != true);
    }

    /// <summary>
    /// FR-017: the entry's missing refs from the docs at the bootstrap point (criteria stay current); a ref that didn't
    /// exist then is left out and reported. <see cref="BaselineRecorder.Complete"/> then adds only missing keys.
    /// </summary>
    private static ComputedEntry Historic(
        Spectra.Core.Models.TestCase test, SourceBaseline baseline, ComputedEntry current, HistoryBootstrap bootstrap,
        List<UnbaselinedRef> unbaselined)
    {
        var entry = new BaselineEntry();
        foreach (var (id, fingerprint) in current.Entry.Criteria)
        {
            entry.Criteria[id] = fingerprint;
        }

        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sourceRef in MissingRefs(test, baseline).Distinct(StringComparer.Ordinal))
        {
            var historic = bootstrap.Result(sourceRef);
            if (historic.Fingerprint is null)
            {
                unbaselined.Add(new UnbaselinedRef(test.Id, sourceRef, historic.Reason!));
                continue;
            }

            entry.Refs[sourceRef] = historic.Fingerprint;
            snapshots[historic.Fingerprint] = historic.Normalised!;
        }

        return new ComputedEntry(entry, snapshots);
    }
}
