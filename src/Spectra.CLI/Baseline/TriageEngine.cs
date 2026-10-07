using Spectra.Core.Baseline;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Baseline;

/// <summary>One ref of a triaged test: its verdict, where it resolved, and (RELINK only) the rename target.</summary>
public sealed record TriagedRef(RefVerdict Verdict, RefSection Section, string? Target = null, string? Submodule = null);

/// <summary>One criterion of a triaged test.</summary>
public sealed record TriagedCriterion(string Id, CriterionState State, string Baseline, string? Current);

/// <summary>
/// A triaged test's full result. Spec 086: <see cref="NoEntry"/> marks a test without a baseline entry that is listed
/// only because one of its refs is RELINK (FR-012); it is not counted as unbaselined.
/// </summary>
public sealed record TriagedTest(
    string Suite,
    string Id,
    Bucket Bucket,
    IReadOnlyList<TriagedRef> Refs,
    IReadOnlyList<TriagedCriterion> Criteria,
    IReadOnlyList<string> UnbaselinedRefs,
    IReadOnlyList<string> UnbaselinedCriteria,
    IReadOnlyDictionary<string, string?> CurrentRefs,
    IReadOnlyDictionary<string, string?> CurrentCriteria,
    TestVerdict Verdict,
    bool NoEntry = false)
{
    public bool Partial => UnbaselinedRefs.Count > 0 || UnbaselinedCriteria.Count > 0;
}

/// <summary>A COSMETIC ref to refresh: its new fingerprint and normalised text.</summary>
public sealed record AutoAck(string Suite, string Id, string Ref, string Fingerprint, string Normalised);

/// <summary>Per-suite results of a triage run.</summary>
public sealed record TriagedSuite(
    string Suite, int TestCount, IReadOnlyList<TriagedTest> Tests, IReadOnlyList<string> UnbaselinedIds, IReadOnlyList<string> PartialIds,
    SuiteScan Scan);

/// <summary>A heading of a changed document that no triaged test references (a generation hint).</summary>
public sealed record NoTestsEntry(string Doc, string Heading);

/// <summary>Everything one triage run found.</summary>
public sealed record TriageRun(
    IReadOnlyList<TriagedSuite> Suites,
    IReadOnlyList<TriageGroup> Groups,
    IReadOnlyList<NoTestsEntry> NoTests,
    IReadOnlyList<AutoAck> AutoAcks,
    IReadOnlyCollection<string> Notes,
    int GroupSize,
    int LightThreshold);

/// <summary>
/// Spec 084 (FR-012–FR-021): computes every test's states against its baseline, buckets, groups and the
/// <c>no_tests</c> hints. Pure apart from reading files; the handler applies auto-acks and writes the outputs.
/// </summary>
public static class TriageEngine
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static async Task<TriageRun> RunAsync(
        string root, SpectraConfig config, string testsPath, IReadOnlyList<string> suites, bool autoAck, bool dryRun,
        List<string> warnings, CancellationToken ct)
    {
        var groupSize = config.Update?.EffectiveGroupSize ?? 10;
        var threshold = config.Update?.EffectiveLightThresholdWords ?? 12;
        var context = await BaselineContext.CreateAsync(root, config, ct);

        var triagedSuites = new List<TriagedSuite>();
        var acks = new List<AutoAck>();
        var allTests = new List<TestCase>();

        foreach (var suite in suites)
        {
            var loaded = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, suite, warnings, ct);
            var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, suite, ct);
            allTests.AddRange(loaded.Tests);

            var triaged = new List<TriagedTest>();
            var unbaselined = new List<string>();
            foreach (var test in loaded.Tests)
            {
                if (!baseline.Tests.TryGetValue(test.Id, out var entry))
                {
                    // Spec 086 FR-012: a test with no entry is evaluated for RELINK only.
                    if (await TriageNoEntryTestAsync(suite, test, context, ct) is { } relink)
                    {
                        triaged.Add(relink);
                    }
                    else
                    {
                        unbaselined.Add(test.Id);
                    }

                    continue;
                }

                // On --dry-run nothing is acked, so COSMETIC refs stay among the changed items.
                var result = await TriageTestAsync(suite, test, entry, context, threshold, autoAck && !dryRun, ct);
                triaged.Add(result);
                acks.AddRange(result.Refs
                    .Where(r => autoAck && r.Verdict.State == RefState.Cosmetic)
                    .Select(r => new AutoAck(suite, test.Id, r.Verdict.Ref, r.Section.Current.Fingerprint!, r.Section.Current.Normalised!)));
            }

            triagedSuites.Add(new TriagedSuite(
                suite, loaded.Tests.Count, triaged, unbaselined, triaged.Where(t => t.Partial && !t.NoEntry).Select(t => t.Id).ToList(),
                loaded.Scan));
        }

        // Spec 087 (F1, FR-004a): one note per submodule that isn't checked out, counting every ref into it.
        foreach (var note in await context.SubmoduleNotesAsync(allTests, ct))
        {
            context.AddNote(note);
        }

        var groups = TriageGrouper.Group(triagedSuites.SelectMany(s => s.Tests).Select(t => t.Verdict), groupSize);
        var noTests = await NoTestsAsync(triagedSuites, allTests, context, ct);
        return new TriageRun(triagedSuites, groups, noTests, acks, context.Notes, groupSize, threshold);
    }

    /// <summary>Triage of one baselined test. COSMETIC refs are left out of the changed items when they are auto-acked.</summary>
    public static async Task<TriagedTest> TriageTestAsync(
        string suite, TestCase test, BaselineEntry entry, BaselineContext context, int threshold, bool cosmeticAcked,
        CancellationToken ct)
    {
        var refs = new List<TriagedRef>();
        var unbaselinedRefs = new List<string>();
        var currentRefs = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            var section = await context.GetSectionAsync(sourceRef, ct);
            currentRefs[sourceRef] = section.Current.Fingerprint;
            if (!entry.Refs.TryGetValue(sourceRef, out var baselineFp))
            {
                // Spec 086 FR-012: a never-baselined ref whose file was renamed is RELINK, not unbaselined.
                if (await context.RelinkCandidateAsync(sourceRef, context.Renames, ct) is { NewRef: not null } unbaselinedCandidate)
                {
                    refs.Add(Relink(sourceRef, null, unbaselinedCandidate, section));
                }
                else
                {
                    unbaselinedRefs.Add(sourceRef);
                }

                continue;
            }

            if (await context.RelinkCandidateAsync(sourceRef, context.Renames, ct) is { } candidate)
            {
                // Spec 086 FR-012/FR-013: a missing file is RELINK when history names its new path; otherwise REMOVED
                // with "not found; <reason>".
                refs.Add(candidate.NewRef is not null
                    ? Relink(sourceRef, baselineFp, candidate, section)
                    : new TriagedRef(new RefVerdict(sourceRef, RefState.Removed, baselineFp, null,
                        [$"{BaselineContext.NotFound}; {candidate.Reason}"], []), section, Submodule: candidate.Submodule));
                continue;
            }

            var snapshot = section.Current.Missing || section.Current.Fingerprint == baselineFp
                ? null
                : await context.GetSnapshotAsync(baselineFp, ct);
            refs.Add(new TriagedRef(ChangeClassifier.ClassifyRef(sourceRef, baselineFp, section.Current, snapshot, threshold), section));
        }

        var criteria = new List<TriagedCriterion>();
        var unbaselinedCriteria = new List<string>();
        var currentCriteria = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var id in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
        {
            var (exists, current) = context.GetCriterion(id);
            currentCriteria[id] = current;
            if (!entry.Criteria.TryGetValue(id, out var baselineFp))
            {
                unbaselinedCriteria.Add(id);
                continue;
            }

            criteria.Add(new TriagedCriterion(id, ChangeClassifier.ClassifyCriterion(baselineFp, exists, current), baselineFp, current));
        }

        return Build(suite, test.Id, refs, criteria, unbaselinedRefs, unbaselinedCriteria, currentRefs, currentCriteria,
            cosmeticAcked, noEntry: false);
    }

    /// <summary>
    /// Spec 086 FR-012 (analysis H1/C6): a test with no baseline entry, evaluated for RELINK only. Null when none of
    /// its refs is RELINK (it stays unbaselined); otherwise the same record as any triaged test — the RELINK refs with
    /// no baseline, every other ref and criterion unbaselined, <c>current</c> filled — marked <see cref="TriagedTest.NoEntry"/>.
    /// </summary>
    public static async Task<TriagedTest?> TriageNoEntryTestAsync(
        string suite, TestCase test, BaselineContext context, CancellationToken ct)
    {
        var refs = new List<TriagedRef>();
        var unbaselinedRefs = new List<string>();
        var currentRefs = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            var section = await context.GetSectionAsync(sourceRef, ct);
            currentRefs[sourceRef] = section.Current.Fingerprint;
            if (await context.RelinkCandidateAsync(sourceRef, context.Renames, ct) is { NewRef: not null } candidate)
            {
                refs.Add(Relink(sourceRef, null, candidate, section));
            }
            else
            {
                unbaselinedRefs.Add(sourceRef);
            }
        }

        if (refs.Count == 0)
        {
            return null;
        }

        var currentCriteria = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        var unbaselinedCriteria = new List<string>();
        foreach (var id in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
        {
            currentCriteria[id] = context.GetCriterion(id).Fingerprint;
            unbaselinedCriteria.Add(id);
        }

        return Build(suite, test.Id, refs, [], unbaselinedRefs, unbaselinedCriteria, currentRefs, currentCriteria,
            cosmeticAcked: false, noEntry: true);
    }

    private static TriagedRef Relink(string sourceRef, string? baseline, RelinkCandidate candidate, RefSection section) =>
        new(new RefVerdict(sourceRef, RefState.Relink, baseline, null, [$"renamed to {candidate.Target}"], []), section, candidate.Target);

    private static TriagedTest Build(
        string suite, string id, List<TriagedRef> refs, List<TriagedCriterion> criteria, List<string> unbaselinedRefs,
        List<string> unbaselinedCriteria, SortedDictionary<string, string?> currentRefs,
        SortedDictionary<string, string?> currentCriteria, bool cosmeticAcked, bool noEntry)
    {
        var bucket = BucketRules.Of(refs.Select(r => r.Verdict.State), criteria.Select(c => c.State));
        var items = new List<ChangedItem>();
        var why = new List<string>();
        var submodules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in refs.Where(r => r.Verdict.State != RefState.Unchanged))
        {
            if (r.Verdict.State == RefState.Cosmetic && cosmeticAcked)
            {
                continue;
            }

            // Spec 087 (F1): every ref into one unchecked-out submodule folds into a single item keyed on the submodule.
            if (r.Submodule is { } submodule)
            {
                if (submodules.Add(submodule))
                {
                    items.Add(new ChangedItem(false, submodule, r.Verdict.State.Wire(), Submodule: true));
                }
            }
            else
            {
                items.Add(new ChangedItem(false, r.Verdict.Ref, r.Verdict.State.Wire(), r.Target));
            }

            why.AddRange(r.Verdict.Why);
        }

        foreach (var c in criteria.Where(c => c.State != CriterionState.Unchanged))
        {
            items.Add(new ChangedItem(true, c.Id, c.State.Wire()));
            why.Add(c.State == CriterionState.Removed ? $"criterion removed: {c.Id}" : $"criterion changed: {c.Id}");
        }

        return new TriagedTest(suite, id, bucket, refs, criteria, unbaselinedRefs, unbaselinedCriteria,
            currentRefs, currentCriteria, new TestVerdict(suite, id, bucket, items, why.Distinct(StringComparer.Ordinal).ToList()),
            noEntry);
    }

    /// <summary>
    /// FR-020 (research R7): for each document with a CHANGED (LOAD or LIGHT) ref, the headings no triaged test
    /// references. A whole-file ref covers every heading; a section ref covers its heading and the headings nested in
    /// it; a heading with a covered descendant is covered too (its section is already partly referenced).
    /// </summary>
    private static async Task<IReadOnlyList<NoTestsEntry>> NoTestsAsync(
        IReadOnlyList<TriagedSuite> suites, IReadOnlyList<TestCase> allTests, BaselineContext context, CancellationToken ct)
    {
        var changedDocs = new Dictionary<string, string>(PathComparer); // full path → repo-relative path
        foreach (var r in suites.SelectMany(s => s.Tests).SelectMany(t => t.Refs))
        {
            if (r.Verdict.State is RefState.Load or RefState.Light && r.Section.FullPath is not null)
            {
                changedDocs.TryAdd(r.Section.FullPath, r.Section.RelativePath!);
            }
        }

        if (changedDocs.Count == 0)
        {
            return [];
        }

        var anchorsByDoc = new Dictionary<string, List<string?>>(PathComparer);
        foreach (var sourceRef in allTests.SelectMany(t => t.SourceRefs).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            var section = await context.GetSectionAsync(sourceRef, ct);
            if (section.FullPath is not null && changedDocs.ContainsKey(section.FullPath))
            {
                if (!anchorsByDoc.TryGetValue(section.FullPath, out var list))
                {
                    anchorsByDoc[section.FullPath] = list = [];
                }

                list.Add(SectionExtractor.AnchorOf(sourceRef));
            }
        }

        var entries = new List<NoTestsEntry>();
        foreach (var (fullPath, relative) in changedDocs.OrderBy(d => d.Value, StringComparer.Ordinal))
        {
            var headings = await context.GetHeadingsAsync(fullPath, ct);
            var covered = new HashSet<int>();
            foreach (var anchor in anchorsByDoc.GetValueOrDefault(fullPath) ?? [])
            {
                covered.UnionWith(SectionExtractor.CoveredHeadings(headings, anchor));
            }

            // A heading whose section contains a covered heading is covered too.
            for (var i = headings.Count - 1; i >= 0; i--)
            {
                for (var j = i + 1; j < headings.Count && headings[j].Level > headings[i].Level; j++)
                {
                    if (covered.Contains(j))
                    {
                        covered.Add(i);
                        break;
                    }
                }
            }

            entries.AddRange(headings.Select((h, i) => (h, i)).Where(x => !covered.Contains(x.i)).Select(x => new NoTestsEntry(relative, x.h.CleanText)));
        }

        return entries;
    }
}
