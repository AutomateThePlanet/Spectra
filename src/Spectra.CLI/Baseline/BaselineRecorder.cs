using Spectra.Core.Baseline;
using Spectra.Core.Models;

namespace Spectra.CLI.Baseline;

/// <summary>A computed baseline entry and the snapshots its ref fingerprints need (fingerprint → normalised text).</summary>
public sealed record ComputedEntry(BaselineEntry Entry, IReadOnlyDictionary<string, string> Snapshots);

/// <summary>What a baseline writer did (or would do on <c>--dry-run</c>).</summary>
public sealed class BaselineWriteSummary
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Completed { get; set; }
    public int Recomputed { get; set; }
    public int AutoAcked { get; set; }
    public int SnapshotsWritten { get; set; }

    /// <summary>Spec 098 (F5): keep-baseline tests without an entry, left unbaselined (no entry created).</summary>
    public int LeftUnbaselined { get; set; }

    /// <summary>Spec 085: how an ingest recorded its entries; emitted as <c>mode</c> when set.</summary>
    public RecordMode? Mode { get; set; }

    /// <summary>The <c>baseline</c> object in a writer's JSON output.</summary>
    public object ToJson()
    {
        var json = new Dictionary<string, object?>
        {
            ["created"] = Created,
            ["updated"] = Updated,
            ["unchanged"] = Unchanged,
            ["snapshots_written"] = SnapshotsWritten
        };
        if (Mode is { } mode)
        {
            json["mode"] = mode == RecordMode.KeepBaseline ? "keep" : "rebuild";
        }

        if (LeftUnbaselined > 0)
        {
            json["left_unbaselined"] = LeftUnbaselined;
        }

        return json;
    }

    public string ToHuman() =>
        $"Baseline: created {Created}, updated {Updated}, unchanged {Unchanged}, snapshots written {SnapshotsWritten}"
        + (LeftUnbaselined > 0 ? $", left unbaselined {LeftUnbaselined}" : "");
}

/// <summary>
/// Spec 085 F5: <see cref="Rebuild"/> recomputes an ingested test's entry (keeping dead refs, F3);
/// <see cref="KeepBaseline"/> (critic repairs) never changes an existing fingerprint and only adds missing keys; Spec 098
/// (F5): it never creates an entry, so a never-baselined test stays unbaselined.
/// </summary>
public enum RecordMode
{
    Rebuild,
    KeepBaseline
}

/// <summary>
/// Spec 084 (FR-024, FR-026, FR-026a, FR-027, research R9): builds a test's baseline entry from current content. Refs
/// that don't resolve (or whose anchor matches no heading) and criterion IDs that exist in no criteria file are left
/// out, so they show as unbaselined at triage instead of carrying a meaningless fingerprint.
/// </summary>
public static class BaselineRecorder
{
    public static async Task<ComputedEntry> ComputeAsync(TestCase test, BaselineContext context, CancellationToken ct)
    {
        var entry = new BaselineEntry();
        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            var section = await context.GetSectionAsync(sourceRef, ct);
            if (section.Current.Missing)
            {
                continue;
            }

            entry.Refs[sourceRef] = section.Current.Fingerprint!;
            snapshots[section.Current.Fingerprint!] = section.Current.Normalised!;
        }

        foreach (var id in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
        {
            var (exists, fingerprint) = context.GetCriterion(id);
            if (exists)
            {
                entry.Criteria[id] = fingerprint!;
            }
        }

        return new ComputedEntry(entry, snapshots);
    }

    /// <summary>
    /// Replaces <paramref name="test"/>'s entry with <paramref name="computed"/> (a full recompute: keys the test no
    /// longer lists go) and counts it as created, updated or unchanged. Spec 085 F3: a ref the test still lists that
    /// doesn't resolve now, and a criterion it still links that no longer exists, keep their previous fingerprint, so
    /// they keep reporting REMOVED / CRITERIA_REMOVED. Returns the snapshots the new entry needs.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Replace(
        SourceBaseline baseline, TestCase test, ComputedEntry computed, BaselineWriteSummary summary)
    {
        var entry = computed.Entry;
        if (baseline.Tests.TryGetValue(test.Id, out var existing))
        {
            foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
            {
                if (!entry.Refs.ContainsKey(sourceRef) && existing.Refs.TryGetValue(sourceRef, out var fingerprint))
                {
                    entry.Refs[sourceRef] = fingerprint;
                }
            }

            foreach (var id in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
            {
                if (!entry.Criteria.ContainsKey(id) && existing.Criteria.TryGetValue(id, out var fingerprint))
                {
                    entry.Criteria[id] = fingerprint;
                }
            }

            if (existing.ContentEquals(entry)) summary.Unchanged++;
            else summary.Updated++;
        }
        else
        {
            summary.Created++;
        }

        baseline.Tests[test.Id] = entry;
        return computed.Snapshots;
    }

    /// <summary>
    /// Adds the keys of <paramref name="computed"/> that <paramref name="id"/>'s entry lacks, never changing an
    /// existing fingerprint or dropping a key (FR-026 / FR-026a). Returns the snapshots of the added refs only.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Complete(
        SourceBaseline baseline, string id, ComputedEntry computed, BaselineWriteSummary summary)
    {
        if (!baseline.Tests.TryGetValue(id, out var existing))
        {
            summary.Created++;
            baseline.Tests[id] = computed.Entry;
            return computed.Snapshots;
        }

        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        var added = false;
        foreach (var (key, fingerprint) in computed.Entry.Refs)
        {
            if (existing.Refs.TryAdd(key, fingerprint))
            {
                added = true;
                snapshots[fingerprint] = computed.Snapshots[fingerprint];
            }
        }

        foreach (var (key, fingerprint) in computed.Entry.Criteria)
        {
            added |= existing.Criteria.TryAdd(key, fingerprint);
        }

        if (added) summary.Completed++;
        else summary.Unchanged++;
        return snapshots;
    }

    /// <summary>
    /// Spec 084 FR-027 / Spec 085: records entries for tests an ingest just persisted (<paramref name="mode"/>: a full
    /// rebuild, or keep-baseline for critic repairs), writes their snapshots, and saves the suite baseline. The suite
    /// is scanned after persisting, so pruning sees the new files and is skipped when any test file fails to parse
    /// (the warning goes to <paramref name="warnings"/>). Runs inside the caller's baseline lock. Spec 099 F1: a caller
    /// that already read the suite passes <paramref name="scan"/> (built with <see cref="SuiteScanBuilder"/> from its
    /// read plus the files it wrote), and the suite is not parsed again.
    /// </summary>
    public static async Task<BaselineWriteSummary> RecordPersistedAsync(
        string root, Spectra.Core.Models.Config.SpectraConfig config, string testsPath, string suite,
        SourceBaseline baseline, IEnumerable<TestCase> persisted, RecordMode mode, List<string> warnings, CancellationToken ct,
        SuiteScan? scan = null)
    {
        var summary = new BaselineWriteSummary { Mode = mode };
        var context = await BaselineContext.CreateAsync(root, config, ct);
        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var test in persisted)
        {
            // Spec 098 (F5, FR-012): a repair never baselines a test — the user decides when its baseline starts.
            if (mode == RecordMode.KeepBaseline && !baseline.Tests.ContainsKey(test.Id))
            {
                summary.LeftUnbaselined++;
                continue;
            }

            var computed = await ComputeAsync(test, context, ct);
            var needed = mode == RecordMode.KeepBaseline
                ? Complete(baseline, test.Id, computed, summary)
                : Replace(baseline, test, computed, summary);
            foreach (var (fingerprint, text) in needed)
            {
                snapshots[fingerprint] = text;
            }
        }

        await WriteSnapshotsAsync(root, snapshots, summary, ct);
        scan ??= await BaselineStore.ScanSuiteAsync(root, testsPath, suite, ct);
        await BaselineStore.SaveSuiteAsync(testsPath, suite, baseline, scan, warnings, ct);
        return summary;
    }

    /// <summary>Writes the snapshots that don't exist yet; counts the ones written.</summary>
    public static async Task WriteSnapshotsAsync(
        string repoRoot, IEnumerable<KeyValuePair<string, string>> snapshots, BaselineWriteSummary summary, CancellationToken ct)
    {
        foreach (var (fingerprint, normalised) in snapshots)
        {
            if (await BaselineStore.EnsureSnapshotAsync(repoRoot, fingerprint, normalised, ct))
            {
                summary.SnapshotsWritten++;
            }
        }
    }

    /// <summary>Counts the snapshots of <paramref name="snapshots"/> that don't exist yet (for <c>--dry-run</c>).</summary>
    public static void CountMissingSnapshots(
        string repoRoot, IEnumerable<KeyValuePair<string, string>> snapshots, BaselineWriteSummary summary)
    {
        foreach (var (fingerprint, _) in snapshots)
        {
            if (!File.Exists(Path.Combine(BaselineStore.SnapshotDir(repoRoot), SourceFingerprint.Hex(fingerprint) + ".txt")))
            {
                summary.SnapshotsWritten++;
            }
        }
    }
}
