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

    /// <summary>The <c>baseline</c> object in a writer's JSON output.</summary>
    public object ToJson() => new Dictionary<string, object?>
    {
        ["created"] = Created,
        ["updated"] = Updated,
        ["unchanged"] = Unchanged,
        ["snapshots_written"] = SnapshotsWritten
    };

    public string ToHuman() =>
        $"Baseline: created {Created}, updated {Updated}, unchanged {Unchanged}, snapshots written {SnapshotsWritten}";
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
    /// Replaces <paramref name="id"/>'s entry with <paramref name="computed"/> (a full recompute: stale keys go) and
    /// counts it as created, updated or unchanged. Returns the snapshots the new entry needs.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Replace(
        SourceBaseline baseline, string id, ComputedEntry computed, BaselineWriteSummary summary)
    {
        if (!baseline.Tests.TryGetValue(id, out var existing))
        {
            summary.Created++;
        }
        else if (existing.ContentEquals(computed.Entry))
        {
            summary.Unchanged++;
        }
        else
        {
            summary.Updated++;
        }

        baseline.Tests[id] = computed.Entry;
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
    /// Spec 084 FR-027: records full entries for tests an ingest just persisted, writes their snapshots, and saves the
    /// suite baseline (pruned to <paramref name="liveIds"/>). Runs inside the caller's baseline lock.
    /// </summary>
    public static async Task<BaselineWriteSummary> RecordPersistedAsync(
        string root, Spectra.Core.Models.Config.SpectraConfig config, string testsPath, string suite,
        SourceBaseline baseline, IEnumerable<TestCase> persisted, IEnumerable<string> liveIds, CancellationToken ct)
    {
        var summary = new BaselineWriteSummary();
        var context = await BaselineContext.CreateAsync(root, config, ct);
        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var test in persisted)
        {
            var computed = await ComputeAsync(test, context, ct);
            foreach (var (fingerprint, text) in Replace(baseline, test.Id, computed, summary))
            {
                snapshots[fingerprint] = text;
            }
        }

        await WriteSnapshotsAsync(root, snapshots, summary, ct);
        await BaselineStore.SaveSuiteAsync(testsPath, suite, baseline, liveIds.ToHashSet(StringComparer.Ordinal), ct);
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
