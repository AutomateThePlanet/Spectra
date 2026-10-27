namespace Spectra.CLI.Generation.Plan;

/// <summary>
/// Spec 094: a behaviour of the analysis with its source resolved. <paramref name="DocPath"/> and
/// <paramref name="Position"/> (−1 = the whole document, else the heading index) group it; <paramref name="Ref"/> is the
/// canonical section ref; an unresolved behaviour has a <paramref name="Reason"/> and no ref. Spec 095:
/// <paramref name="CoveredBy"/> is the existing test that covers it (null when uncovered).
/// </summary>
public sealed record ResolvedBehavior(
    string Title, string Category, string Technique, string Source, string? Ref, string? DocPath, int Position, string? Reason,
    string? CoveredBy = null);

/// <summary>
/// Spec 094 (FR-002–FR-005; R3, R4): builds the generation plan. Behaviours get ids <c>B1…</c> in input order. Resolved
/// ones are ordered by document (ordinal path), then section (whole document first, then heading order), then input
/// order, and packed into batches: a batch never spans two documents, holds at most <c>batchSize</c> behaviours, and
/// takes a whole section when it fits (a section larger than the batch size fills consecutive batches by itself).
/// Unresolved behaviours form the last batch, <c>unsourced</c>. Every batch gets a count ≥ 1: 1 plus a share of
/// <c>recommended − n</c> by its number of behaviours (largest remainder, ties to the earlier batch), so the counts add up
/// to <c>recommended</c>; when <c>recommended</c> is smaller than the number of batches, every batch gets 1.
/// Spec 095 (FR-006): only uncovered behaviours (<c>CoveredBy == null</c>) are batched — covered ones keep their ids and
/// <c>covered_by</c> in the plan but are in no batch (not even <c>unsourced</c>), and <c>sections</c> keeps only the refs
/// batches use. When <c>recommended</c> equals the number of batched behaviours (always, as the analysis ingest passes
/// it), each batch's count is its number of behaviours; the share rule and <c>one_per_batch</c> can't be reached from the
/// ingest.
/// </summary>
public static class GenerationPlanBuilder
{
    public static GenerationPlan Build(
        string suite,
        string? focus,
        int batchSize,
        int recommended,
        IReadOnlyList<ResolvedBehavior> behaviors,
        IReadOnlyDictionary<string, string> sections,
        IReadOnlyList<string> notes,
        double? coverageThreshold = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var planned = behaviors.Select((b, i) => new PlannedBehavior(
            $"B{i + 1}", b.Title, b.Category, b.Technique, b.Source, b.Ref, b.Ref is not null, b.Ref is null ? b.Reason : null,
            b.CoveredBy)).ToList();

        var groups = behaviors
            .Select((b, i) => (Behavior: b, Id: planned[i].Id, Order: i))
            .Where(x => x.Behavior.Ref is not null && x.Behavior.CoveredBy is null)
            .OrderBy(x => x.Behavior.DocPath, StringComparer.Ordinal)
            .ThenBy(x => x.Behavior.Position)
            .ThenBy(x => x.Order)
            .GroupBy(x => (x.Behavior.DocPath, x.Behavior.Position))
            .ToList();

        var members = new List<List<(string Id, string Ref)>>();
        List<(string Id, string Ref)>? current = null;
        string? currentDoc = null;
        foreach (var group in groups)
        {
            var section = group.Select(x => (x.Id, x.Behavior.Ref!)).ToList();
            if (current is not null
                && (currentDoc != group.Key.DocPath || (current.Count > 0 && current.Count + section.Count > batchSize)))
            {
                members.Add(current);
                current = null;
            }

            currentDoc = group.Key.DocPath;
            foreach (var item in section)
            {
                current ??= [];
                current.Add(item);
                if (current.Count == batchSize)
                {
                    members.Add(current);
                    current = null;
                }
            }
        }

        if (current is { Count: > 0 })
        {
            members.Add(current);
        }

        var batchMembers = members
            .Select((m, i) => ((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), m))
            .ToList();
        var unsourced = planned.Where(b => !b.Resolved && b.CoveredBy is null).Select(b => (b.Id, Ref: (string?)null)).ToList();

        var weights = batchMembers.Select(b => b.m.Count).ToList();
        if (unsourced.Count > 0)
        {
            weights.Add(unsourced.Count);
        }

        var (counts, rule) = Counts(weights, recommended);

        var batches = batchMembers.Select((b, i) => new PlannedBatch(
            b.Item1,
            b.m.Select(x => x.Id).ToList(),
            b.m.Select(x => x.Ref).Distinct(StringComparer.Ordinal).ToList(),
            counts[i])).ToList();
        if (unsourced.Count > 0)
        {
            batches.Add(new PlannedBatch(GenerationPlan.Unsourced, unsourced.Select(x => x.Id).ToList(), [], counts[^1]));
        }

        var used = batches.SelectMany(b => b.SourceRefs).ToHashSet(StringComparer.Ordinal);
        return new GenerationPlan(
            suite, focus, batchSize, recommended, counts.Sum(), rule, planned,
            sections.Where(s => used.Contains(s.Key)).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal),
            batches, notes, coverageThreshold);
    }

    /// <summary>The count rule (R3) over the batches' behaviour counts.</summary>
    public static (IReadOnlyList<int> Counts, string Rule) Counts(IReadOnlyList<int> weights, int recommended)
    {
        var n = weights.Count;
        if (n == 0)
        {
            return ([], GenerationPlan.Proportional);
        }

        if (recommended < n)
        {
            return (Enumerable.Repeat(1, n).ToList(), GenerationPlan.OnePerBatch);
        }

        // Spec 095 (FR-006, analyze M1): recommended = the batched behaviours (always, from the analysis ingest) → one test
        // per behaviour, so each batch's count is exactly its number of behaviours (the share rule below would not give that
        // for unequal batches, e.g. 4 and 1 → 3 and 2).
        if (recommended == weights.Sum())
        {
            return (weights.ToList(), GenerationPlan.Proportional);
        }

        var extra = recommended - n;
        var total = weights.Sum();
        var counts = new int[n];
        var remainders = new (long Numerator, int Index)[n];
        var given = 0;
        for (var i = 0; i < n; i++)
        {
            var numerator = (long)extra * weights[i];
            var share = (int)(numerator / total);
            counts[i] = 1 + share;
            given += share;
            remainders[i] = (numerator % total, i);
        }

        foreach (var (_, index) in remainders.OrderByDescending(r => r.Numerator).ThenBy(r => r.Index).Take(extra - given))
        {
            counts[index]++;
        }

        return (counts, GenerationPlan.Proportional);
    }
}
