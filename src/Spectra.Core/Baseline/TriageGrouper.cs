namespace Spectra.Core.Baseline;

/// <summary>
/// A changed ref or criterion of a test, with its wire state (<c>LOAD</c>, <c>CRITERIA_CHANGED</c>, …). Spec 086: a
/// RELINK ref carries its <see cref="Target"/> (the repo-relative path it was renamed to), which is part of the
/// signature, so RELINK groups split by target.
/// </summary>
public sealed record ChangedItem(bool IsCriterion, string Key, string State, string? Target = null, bool Submodule = false)
{
    /// <summary>Spec 087 (F1): a submodule item stands for every ref of the test into that submodule (Key = its path).</summary>
    public string Signature => Submodule
        ? $"submodule:{Key}={State}"
        : $"{(IsCriterion ? "criterion" : "ref")}:{Key}={State}{(Target is null ? "" : $"→{Target}")}";
}

/// <summary>One test's triage result: its bucket, the items that are not unchanged, and why.</summary>
public sealed record TestVerdict(string Suite, string Id, Bucket Bucket, IReadOnlyList<ChangedItem> Items, IReadOnlyList<string> Why);

/// <summary>Tests in one suite and bucket sharing the same changed items, chunked; <see cref="Id"/> is <c>G1</c>, <c>G2</c>, … .</summary>
public sealed record TriageGroup(
    string Id, string Suite, Bucket Bucket, IReadOnlyList<ChangedItem> Items, IReadOnlyList<string> Tests, IReadOnlyList<string> Why);

/// <summary>
/// Spec 084 (FR-017, research R8): tests with the same suite, bucket and set of changed items form one group, chunked
/// at the group size in test-ID order. Groups are ordered by suite, then bucket (worst first), then the changed-item
/// signature, then chunk, and numbered across the whole run. COSMETIC and UNCHANGED tests form no groups.
/// </summary>
public static class TriageGrouper
{
    public static IReadOnlyList<TriageGroup> Group(IEnumerable<TestVerdict> verdicts, int groupSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groupSize, 1);

        var keyed = verdicts
            .Where(v => v.Bucket > Bucket.Cosmetic)
            .Select(v => (Verdict: v, Items: v.Items.OrderBy(i => i.Signature, StringComparer.Ordinal).ToList()))
            .GroupBy(x => (x.Verdict.Suite, x.Verdict.Bucket, Signature: string.Join("\n", x.Items.Select(i => i.Signature))))
            .OrderBy(g => g.Key.Suite, StringComparer.Ordinal)
            .ThenByDescending(g => (int)g.Key.Bucket)
            .ThenBy(g => g.Key.Signature, StringComparer.Ordinal);

        var groups = new List<TriageGroup>();
        foreach (var group in keyed)
        {
            var members = group.OrderBy(x => x.Verdict.Id, StringComparer.Ordinal).ToList();
            var items = members[0].Items;

            // Spec 087 (F1): a group of submodule items only (a setup problem) is never chunked: one per suite and submodule.
            var size = items.Count > 0 && items.All(i => i.Submodule) ? members.Count : groupSize;
            foreach (var chunk in members.Chunk(size))
            {
                var why = chunk.SelectMany(x => x.Verdict.Why).Distinct(StringComparer.Ordinal).ToList();
                groups.Add(new TriageGroup(
                    $"G{groups.Count + 1}", group.Key.Suite, group.Key.Bucket, items,
                    chunk.Select(x => x.Verdict.Id).ToList(), why));
            }
        }

        return groups;
    }
}
