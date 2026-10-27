using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>Spec 084 T4 (FR-015, FR-017): bucket precedence and grouping.</summary>
public class BucketAndGroupingTests
{
    private static readonly Bucket[] Precedence =
    [
        Bucket.Relink, Bucket.Removed, Bucket.CriteriaRemoved, Bucket.Load, Bucket.CriteriaChanged, Bucket.Light, Bucket.Cosmetic, Bucket.Unchanged
    ];

    public static IEnumerable<object[]> Pairs() =>
        from a in Precedence from b in Precedence select new object[] { a, b };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void TheWorstStateWins(Bucket a, Bucket b)
    {
        var expected = Array.IndexOf(Precedence, a) <= Array.IndexOf(Precedence, b) ? a : b;

        var (refs, criteria) = Split(a, b);

        Assert.Equal(expected, BucketRules.Of(refs, criteria));
    }

    [Fact]
    public void NoItems_IsUnchanged() => Assert.Equal(Bucket.Unchanged, BucketRules.Of([], []));

    [Fact]
    public void WorstFirst_IsThePrecedenceOrder() => Assert.Equal(Precedence, BucketRules.WorstFirst);

    [Theory]
    [InlineData(Bucket.Relink, "RELINK")]
    [InlineData(Bucket.Removed, "REMOVED")]
    [InlineData(Bucket.CriteriaRemoved, "CRITERIA_REMOVED")]
    [InlineData(Bucket.Load, "LOAD")]
    [InlineData(Bucket.CriteriaChanged, "CRITERIA_CHANGED")]
    [InlineData(Bucket.Light, "LIGHT")]
    [InlineData(Bucket.Cosmetic, "COSMETIC")]
    [InlineData(Bucket.Unchanged, "UNCHANGED")]
    public void WireNames(Bucket bucket, string wire) => Assert.Equal(wire, bucket.Wire());

    private static (List<RefState>, List<CriterionState>) Split(params Bucket[] buckets)
    {
        var refs = new List<RefState>();
        var criteria = new List<CriterionState>();
        foreach (var bucket in buckets)
        {
            switch (bucket)
            {
                case Bucket.Relink: refs.Add(RefState.Relink); break;
                case Bucket.Removed: refs.Add(RefState.Removed); break;
                case Bucket.Load: refs.Add(RefState.Load); break;
                case Bucket.Light: refs.Add(RefState.Light); break;
                case Bucket.Cosmetic: refs.Add(RefState.Cosmetic); break;
                case Bucket.Unchanged: refs.Add(RefState.Unchanged); break;
                case Bucket.CriteriaRemoved: criteria.Add(CriterionState.Removed); break;
                case Bucket.CriteriaChanged: criteria.Add(CriterionState.Changed); break;
            }
        }

        return (refs, criteria);
    }

    // ---- grouping ----

    private static readonly ChangedItem QuotasLoad = new(false, "docs/s.md#Quotas", "LOAD");
    private static readonly ChangedItem LimitsLoad = new(false, "docs/s.md#Limits", "LOAD");

    private static TestVerdict V(string suite, string id, Bucket bucket, string why, params ChangedItem[] items) =>
        new(suite, id, bucket, items, [why]);

    [Fact]
    public void SameItemsAndBucket_FormOneGroup_InIdOrder()
    {
        var groups = TriageGrouper.Group(
            [V("s", "TC-2", Bucket.Load, "numbers: 1→2", LimitsLoad, QuotasLoad), V("s", "TC-1", Bucket.Load, "numbers: 1→2", QuotasLoad, LimitsLoad)],
            10);

        var group = Assert.Single(groups);
        Assert.Equal("G1", group.Id);
        Assert.Equal(["TC-1", "TC-2"], group.Tests);
        Assert.Equal(["numbers: 1→2"], group.Why);
    }

    [Fact]
    public void SameRefs_DifferentBucket_AreSeparateGroups()
    {
        var light = new ChangedItem(false, "docs/s.md#Quotas", "LIGHT");

        var groups = TriageGrouper.Group([V("s", "TC-1", Bucket.Load, "a", QuotasLoad), V("s", "TC-2", Bucket.Light, "b", light)], 10);

        Assert.Equal(2, groups.Count);
        Assert.Equal(Bucket.Load, groups[0].Bucket);
        Assert.Equal(Bucket.Light, groups[1].Bucket);
    }

    [Fact]
    public void Chunking_AtTheGroupSize()
    {
        var verdicts = Enumerable.Range(1, 10).Select(i => V("s", $"TC-{i:D2}", Bucket.Load, "w", QuotasLoad));

        var groups = TriageGrouper.Group(verdicts, 4);

        Assert.Equal([4, 4, 2], groups.Select(g => g.Tests.Count));
        Assert.Equal(["G1", "G2", "G3"], groups.Select(g => g.Id));
        Assert.Equal("TC-01", groups[0].Tests[0]);
        Assert.Equal("TC-09", groups[2].Tests[0]);
    }

    [Fact]
    public void Order_IsSuite_ThenWorstBucket_ThenSignature_NumberedAcrossSuites()
    {
        var removed = new ChangedItem(false, "docs/z.md#Gone", "REMOVED");
        var groups = TriageGrouper.Group(
        [
            V("b", "TC-9", Bucket.Load, "w", QuotasLoad),
            V("a", "TC-3", Bucket.Load, "w", QuotasLoad),
            V("a", "TC-4", Bucket.Load, "w", LimitsLoad),
            V("a", "TC-5", Bucket.Removed, "anchor not found", removed)
        ], 10);

        Assert.Equal(["a/REMOVED/TC-5", "a/LOAD/TC-4", "a/LOAD/TC-3", "b/LOAD/TC-9"],
            groups.Select(g => $"{g.Suite}/{g.Bucket.Wire()}/{string.Join(",", g.Tests)}"));
        Assert.Equal(["G1", "G2", "G3", "G4"], groups.Select(g => g.Id));
    }

    [Fact]
    public void RelinkGroups_SplitByTarget()
    {
        // Spec 086 FR-013: the same missing ref renamed to different targets (e.g. via different source dirs) is two groups.
        var toB = new ChangedItem(false, "docs/a.md#X", "RELINK", "docs/b.md");
        var toC = new ChangedItem(false, "docs/a.md#X", "RELINK", "docs/c.md");

        var groups = TriageGrouper.Group(
            [V("s", "TC-1", Bucket.Relink, "renamed to docs/b.md", toB), V("s", "TC-2", Bucket.Relink, "renamed to docs/c.md", toC),
             V("s", "TC-3", Bucket.Relink, "renamed to docs/b.md", toB)], 10);

        Assert.Equal(["TC-1,TC-3", "TC-2"], groups.Select(g => string.Join(",", g.Tests)));
        Assert.All(groups, g => Assert.Equal(Bucket.Relink, g.Bucket));
        Assert.Equal("ref:docs/a.md#X=RELINK→docs/b.md", groups[0].Items[0].Signature);
    }

    [Fact]
    public void SubmoduleGroups_AreNotChunked_OtherGroupsAre()
    {
        // Spec 087 F1 (FR-003): one group per suite and submodule that isn't checked out, whatever the group size.
        var submodule = new ChangedItem(false, "docs/sub", "REMOVED", Submodule: true);
        var verdicts = Enumerable.Range(1, 5).Select(i => V("s", $"TC-{i}", Bucket.Removed, "not found; submodule not checked out", submodule))
            .Concat(Enumerable.Range(6, 5).Select(i => V("s", $"TC-{i}", Bucket.Load, "w", QuotasLoad)));

        var groups = TriageGrouper.Group(verdicts, 2);

        Assert.Equal([5, 2, 2, 1], groups.Select(g => g.Tests.Count));
        Assert.Equal("submodule:docs/sub=REMOVED", groups[0].Items[0].Signature);
    }

    [Fact]
    public void CosmeticAndUnchanged_FormNoGroups()
    {
        var cosmetic = new ChangedItem(false, "docs/s.md#Quotas", "COSMETIC");

        Assert.Empty(TriageGrouper.Group([V("s", "TC-1", Bucket.Cosmetic, "", cosmetic), V("s", "TC-2", Bucket.Unchanged, "")], 10));
    }

    [Fact]
    public void Why_IsTheUnionOfTheMembers_InOrder()
    {
        var groups = TriageGrouper.Group([V("s", "TC-1", Bucket.Load, "numbers: 1→2", QuotasLoad), V("s", "TC-2", Bucket.Load, "modal: must→should", QuotasLoad)], 10);

        Assert.Equal(["numbers: 1→2", "modal: must→should"], Assert.Single(groups).Why);
    }
}
