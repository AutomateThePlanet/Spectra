using Spectra.CLI.Baseline;
using Spectra.Core.Baseline;
using Spectra.Core.Models;

namespace Spectra.CLI.Generation;

/// <summary>Spec 088: what a section block of the update prompt says about a ref.</summary>
public enum SectionState
{
    Unchanged,
    Changed,
    NotBaselined,
    Removed,
    Relink
}

/// <summary>
/// Spec 088: a linked criterion's state in the update prompt. Declared in precedence order: across a group, a criterion
/// takes the highest state any linking test gives it (Spec 089 F4).
/// </summary>
public enum LinkedCriterionState
{
    Unchanged,

    /// <summary>Spec 089 (F4): no baseline value to compare with — no entry, or the entry lacks the criterion.</summary>
    NotBaselined,
    ChangedSinceBaseline,
    Removed
}

/// <summary>
/// Spec 088 (FR-001–FR-004): one ref (as written) at one baseline fingerprint — its current raw section, the previous
/// normalised version, and what changed; or, when it doesn't resolve, the reason. <see cref="Tests"/> lists the tests
/// it applies to.
/// </summary>
public sealed record SectionBlock(
    string Label,
    string Ref,
    IReadOnlyList<string> Tests,
    SectionState State,
    string? Bucket,
    string? Current,
    string? Previous,
    string? PreviousUnavailableReason,
    IReadOnlyList<string> Why,
    IReadOnlyList<DiffRun> Diff,
    string? Reason);

/// <summary>Spec 088 (FR-005): a criterion the tests link, with its current text (null when removed).</summary>
public sealed record CriterionBlock(string Id, string? Text, LinkedCriterionState State);

/// <summary>Spec 088: one test to review, with the section labels and criterion IDs it links.</summary>
public sealed record TestBlock(TestCase Test, IReadOnlyList<string> SectionLabels, IReadOnlyList<string> CriterionIds);

/// <summary>Spec 088: everything one update prompt carries. <see cref="NothingToReconcile"/> names tests with nothing to edit against.</summary>
public sealed record UpdateContext(
    IReadOnlyList<TestBlock> Tests,
    IReadOnlyList<SectionBlock> Sections,
    IReadOnlyList<CriterionBlock> Criteria,
    IReadOnlyList<string> NothingToReconcile);

/// <summary>
/// Spec 088 (research R1): builds the update prompt's context for one test or a triage group, from the same machinery
/// triage uses — the source-ref resolver and 085 matcher, the snapshots, <see cref="ChangeClassifier"/>, and relink
/// candidates — so the prompt shows exactly what triage saw. Only the criteria the tests link appear; never a
/// suite-wide or repository-wide list.
/// </summary>
public static class UpdateContextBuilder
{
    public static async Task<UpdateContext> BuildAsync(
        IReadOnlyList<TestCase> tests, SourceBaseline baseline, BaselineContext context, int lightThreshold,
        CancellationToken ct)
    {
        var ordered = tests
            .OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();

        // Sections keyed by (ref as written, baseline fingerprint), labelled in order of first appearance.
        var keys = new List<(string Ref, string? Fingerprint)>();
        var index = new Dictionary<(string Ref, string? Fingerprint), int>();
        var sectionTests = new List<List<string>>();
        var testSections = new List<List<string>>();
        var criterionIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var criterionStates = new Dictionary<string, LinkedCriterionState>(StringComparer.OrdinalIgnoreCase);
        var testCriteria = new List<List<string>>();

        foreach (var test in ordered)
        {
            baseline.Tests.TryGetValue(test.Id, out var entry);
            var labels = new List<string>();
            foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
            {
                string? fingerprint = null;
                entry?.Refs.TryGetValue(sourceRef, out fingerprint);
                var key = (sourceRef, fingerprint);
                if (!index.TryGetValue(key, out var i))
                {
                    i = keys.Count;
                    index[key] = i;
                    keys.Add(key);
                    sectionTests.Add([]);
                }

                sectionTests[i].Add(test.Id);
                labels.Add(Label(i));
            }

            testSections.Add(labels);

            var linked = new List<string>();
            foreach (var raw in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                var id = raw.Trim();
                if (linked.Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                linked.Add(id);
                criterionIds.Add(id);
                // Spec 089 (F4): this test's view of the criterion; across tests the highest state wins.
                var (exists, current) = context.GetCriterion(id);
                var state = !exists ? LinkedCriterionState.Removed
                    : entry is null || !entry.Criteria.TryGetValue(id, out var was) ? LinkedCriterionState.NotBaselined
                    : string.Equals(was, current, StringComparison.Ordinal) ? LinkedCriterionState.Unchanged
                    : LinkedCriterionState.ChangedSinceBaseline;
                if (!criterionStates.TryGetValue(id, out var seen) || state > seen)
                {
                    criterionStates[id] = state;
                }
            }

            testCriteria.Add(linked);
        }

        var sections = new List<SectionBlock>(keys.Count);
        for (var i = 0; i < keys.Count; i++)
        {
            sections.Add(await DescribeAsync(Label(i), keys[i].Ref, keys[i].Fingerprint, sectionTests[i], context, lightThreshold, ct));
        }

        var criteria = criterionIds.Select(id => criterionStates[id] == LinkedCriterionState.Removed
                ? new CriterionBlock(id, null, LinkedCriterionState.Removed)
                : new CriterionBlock(id, context.GetCriterionText(id), criterionStates[id]))
            .ToList();

        var blocks = new List<TestBlock>(ordered.Count);
        var nothing = new List<string>();
        for (var t = 0; t < ordered.Count; t++)
        {
            blocks.Add(new TestBlock(ordered[t], testSections[t], testCriteria[t]));

            // FR-006: nothing to reconcile = no resolvable ref and no linked criterion that exists.
            var resolvable = testSections[t].Any(label => sections[LabelIndex(label)].Current is not null);
            var anyCriterion = testCriteria[t].Any(id => context.GetCriterion(id).Exists);
            if (!resolvable && !anyCriterion)
            {
                nothing.Add(ordered[t].Id);
            }
        }

        return new UpdateContext(blocks, sections, criteria, nothing);
    }

    private static async Task<SectionBlock> DescribeAsync(
        string label, string sourceRef, string? baselineFingerprint, IReadOnlyList<string> tests, BaselineContext context,
        int lightThreshold, CancellationToken ct)
    {
        var section = await context.GetSectionAsync(sourceRef, ct);
        if (section.Current.Missing)
        {
            var reason = section.Current.MissingReason ?? BaselineContext.NotFound;
            if (reason == BaselineContext.NotFound && await context.RelinkCandidateAsync(sourceRef, context.Renames, ct) is { } candidate)
            {
                return candidate.NewRef is not null
                    ? Unresolved(label, sourceRef, tests, SectionState.Relink, $"renamed to {candidate.Target}")
                    : Unresolved(label, sourceRef, tests, SectionState.Removed, $"{BaselineContext.NotFound}; {candidate.Reason}");
            }

            return Unresolved(label, sourceRef, tests, SectionState.Removed, reason);
        }

        var current = await context.GetRawSectionAsync(sourceRef, ct);
        if (baselineFingerprint is null)
        {
            return new SectionBlock(label, sourceRef, tests, SectionState.NotBaselined, null, current, null,
                "not baselined", [], [], null);
        }

        if (string.Equals(baselineFingerprint, section.Current.Fingerprint, StringComparison.Ordinal))
        {
            return new SectionBlock(label, sourceRef, tests, SectionState.Unchanged, null, current, null, null, [], [], null);
        }

        var snapshot = await context.GetSnapshotAsync(baselineFingerprint, ct);
        var verdict = ChangeClassifier.ClassifyRef(sourceRef, baselineFingerprint, section.Current, snapshot, lightThreshold);
        return new SectionBlock(label, sourceRef, tests, SectionState.Changed, verdict.State.Wire(), current, snapshot,
            snapshot is null ? "no snapshot" : null, verdict.Why, verdict.Diff, null);
    }

    private static SectionBlock Unresolved(string label, string sourceRef, IReadOnlyList<string> tests, SectionState state, string reason) =>
        new(label, sourceRef, tests, state, null, null, null, null, [], [], reason);

    private static string Label(int index) => $"S{index + 1}";

    private static int LabelIndex(string label) => int.Parse(label.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture) - 1;
}
