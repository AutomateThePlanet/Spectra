using Spectra.CLI.Generation.Plan;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Generation.Plan;

/// <summary>
/// Spec 094 T2 (FR-002–FR-005; R3, R4): the generation plan — behaviour ids in input order, batches packed by section in
/// document order up to the batch size and never across documents, an <c>unsourced</c> batch last, counts that never
/// drop a batch, and a strict, byte-stable serialisation. On 55b541f the plan didn't exist.
/// </summary>
public sealed class GenerationPlanBuilderTests
{
    /// <summary>A resolved behaviour of <paramref name="doc"/> at heading <paramref name="position"/> (-1 = whole doc).</summary>
    private static ResolvedBehavior At(string doc, int position, string title, string? anchor = null) =>
        new(title, "happy_path", "EP", anchor is null ? doc : $"{doc}#{anchor}",
            anchor is null ? doc : $"{doc}#{anchor}", doc, position, null);

    private static ResolvedBehavior Unresolved(string title, string source, string reason) =>
        new(title, "negative", "EG", source, null, null, 0, reason);

    private static GenerationPlan Build(IReadOnlyList<ResolvedBehavior> behaviors, int recommended, int batchSize = 8) =>
        GenerationPlanBuilder.Build("storage", null, batchSize, recommended, behaviors,
            behaviors.Where(b => b.Ref is not null).Select(b => b.Ref!).Distinct()
                .ToDictionary(r => r, r => "sha256:" + new string('0', 64)),
            []);

    private static string[] Ids(PlannedBatch batch) => [.. batch.BehaviorIds];

    [Fact]
    public void Behaviours_GetIdsInInputOrder()
    {
        var plan = Build([At("docs/b.md", 1, "one", "x"), Unresolved("two", "docs/nope.md", "not found"), At("docs/a.md", -1, "three")], 3);

        Assert.Equal(["B1", "B2", "B3"], plan.Behaviors.Select(b => b.Id));
        Assert.Equal(["one", "two", "three"], plan.Behaviors.Select(b => b.Title));
        Assert.False(plan.Behaviors[1].Resolved);
        Assert.Equal("not found", plan.Behaviors[1].Reason);
    }

    [Fact]
    public void Batches_FollowDocumentThenSectionOrder_WholeDocumentFirst()
    {
        var plan = Build(
        [
            At("docs/b.md", 3, "b3", "late"),
            At("docs/a.md", 2, "a2", "second"),
            At("docs/b.md", 1, "b1", "early"),
            At("docs/a.md", -1, "a-whole"),
            At("docs/a.md", 1, "a1", "first"),
        ], 5, batchSize: 2);

        Assert.Equal(["1", "2", "3"], plan.Batches.Select(b => b.BatchId));
        Assert.Equal(["B4", "B5"], Ids(plan.Batches[0]));   // a.md whole doc, then a.md#first
        Assert.Equal(["B2"], Ids(plan.Batches[1]));         // a.md#second; never packed with b.md
        Assert.Equal(["B3", "B1"], Ids(plan.Batches[2]));   // b.md#early, b.md#late
        Assert.Equal(["docs/a.md", "docs/a.md#first"], plan.Batches[0].SourceRefs);
    }

    [Fact]
    public void Sections_ArePackedUpToTheBatchSize_AndNotSplitWhenTheyFit()
    {
        var plan = Build(
        [
            At("docs/a.md", 1, "s1-1", "s1"), At("docs/a.md", 1, "s1-2", "s1"),
            At("docs/a.md", 2, "s2-1", "s2"), At("docs/a.md", 2, "s2-2", "s2"),
            At("docs/a.md", 3, "s3-1", "s3"),
        ], 5, batchSize: 3);

        // s1 (2) fits; s2 (2) would overflow 3 → new batch; s3 (1) fits with s2.
        Assert.Equal(2, plan.Batches.Count);
        Assert.Equal(["B1", "B2"], Ids(plan.Batches[0]));
        Assert.Equal(["B3", "B4", "B5"], Ids(plan.Batches[1]));
        Assert.Equal(3, plan.BatchSize);
    }

    [Fact]
    public void ASectionLargerThanTheBatchSize_FillsConsecutiveBatchesByItself()
    {
        var plan = Build(
        [
            At("docs/a.md", 1, "1", "big"), At("docs/a.md", 1, "2", "big"), At("docs/a.md", 1, "3", "big"),
            At("docs/a.md", 1, "4", "big"), At("docs/a.md", 1, "5", "big"),
        ], 5, batchSize: 2);

        Assert.Equal([2, 2, 1], plan.Batches.Select(b => b.BehaviorIds.Count));
        Assert.All(plan.Batches, b => Assert.Equal(["docs/a.md#big"], b.SourceRefs));
    }

    [Fact]
    public void TheDefaultBatchSize_IsEight()
    {
        var behaviors = Enumerable.Range(1, 9).Select(i => At("docs/a.md", i, $"t{i}", $"s{i}")).ToList();

        var plan = GenerationPlanBuilder.Build("storage", null, 8, 9, behaviors, new Dictionary<string, string>(), []);

        Assert.Equal([8, 1], plan.Batches.Select(b => b.BehaviorIds.Count));
    }

    [Fact]
    public void UnresolvedBehaviours_FormOneUnsourcedBatch_Last_InInputOrder()
    {
        var plan = Build(
        [
            Unresolved("u1", "", "empty ref"),
            At("docs/a.md", 1, "r1", "x"),
            Unresolved("u2", "docs/a.md#missing", "anchor not found"),
        ], 3);

        Assert.Equal(["1", "unsourced"], plan.Batches.Select(b => b.BatchId));
        Assert.Equal(["B1", "B3"], Ids(plan.Batches[1]));
        Assert.Empty(plan.Batches[1].SourceRefs);
    }

    [Fact]
    public void Counts_AddUpToRecommended_WhenThereAreEnough_ProportionalWithLargestRemainder()
    {
        // 20 behaviours over 5 batches of 4 (batch size 4), recommended 12: 1 each + 7 shared by weight 4/20 each
        // = 1.4 → floor 1 each (5), 2 left → the two earliest batches.
        var behaviors = Enumerable.Range(1, 20).Select(i => At("docs/a.md", i, $"t{i}", $"s{i}")).ToList();

        var plan = Build(behaviors, 12, batchSize: 4);

        Assert.Equal([3, 3, 2, 2, 2], plan.Batches.Select(b => b.Count));
        Assert.Equal(12, plan.PlannedTotal);
        Assert.Equal(GenerationPlan.Proportional, plan.CountRule);
    }

    [Fact]
    public void Counts_FollowTheBehaviourWeights()
    {
        var plan = Build(
        [
            .. Enumerable.Range(1, 6).Select(i => At("docs/a.md", 1, $"a{i}", "big")),
            At("docs/b.md", 1, "b1", "small"),
        ], 9, batchSize: 8);

        // n = 2, extra 7 split 6:1 → 6 and 1 → counts 7 and 2.
        Assert.Equal([7, 2], plan.Batches.Select(b => b.Count));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void Counts_AreRaisedToOnePerBatch_WhenRecommendedIsSmallerThanTheBatches(int recommended)
    {
        var behaviors = Enumerable.Range(1, 5).Select(i => At($"docs/d{i}.md", 1, $"t{i}", "s")).ToList();

        var plan = Build(behaviors, recommended);

        Assert.Equal(5, plan.Batches.Count);
        Assert.All(plan.Batches, b => Assert.Equal(1, b.Count));
        Assert.Equal(5, plan.PlannedTotal);
        Assert.Equal(GenerationPlan.OnePerBatch, plan.CountRule);
        Assert.Equal(recommended, plan.Recommended);
    }

    [Fact]
    public void NoBatchWithBehaviours_IsEverDroppedOrZero()
    {
        var behaviors = new List<ResolvedBehavior>();
        for (var d = 0; d < 7; d++)
        {
            behaviors.AddRange(Enumerable.Range(0, d == 0 ? 12 : 1).Select(i => At($"docs/d{d}.md", 1, $"t{d}-{i}", "s")));
        }

        behaviors.Add(Unresolved("u", "x.md", "not found"));
        var plan = Build(behaviors, 9, batchSize: 8);

        Assert.Equal(behaviors.Count, plan.Batches.Sum(b => b.BehaviorIds.Count));
        Assert.All(plan.Batches, b => Assert.True(b.Count >= 1));
        Assert.Equal(9, plan.PlannedTotal);
    }

    [Fact]
    public void NoBehaviours_GiveNoBatches()
    {
        var plan = Build([], 0);

        Assert.Empty(plan.Batches);
        Assert.Equal(0, plan.PlannedTotal);
    }

    [Fact]
    public void ThePlan_SerialisesByteIdentically_AndParsesBack()
    {
        var behaviors = new[] { At("docs/b.md", 1, "b", "x"), At("docs/a.md", -1, "a"), Unresolved("u", "", "empty ref") };
        var first = GenerationPlan.Serialize(Build(behaviors, 3));
        var second = GenerationPlan.Serialize(Build(behaviors, 3));

        Assert.Equal(first, second);
        Assert.DoesNotContain("\r", first);
        Assert.Equal(first, GenerationPlan.Serialize(GenerationPlan.Parse(first)));
        Assert.True(first.IndexOf("\"version\"", StringComparison.Ordinal) < first.IndexOf("\"suite\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"version\":2}")]
    [InlineData("{not json")]
    [InlineData("{\"version\":1,\"suite\":\"s\"}")]
    [InlineData("{\"version\":1,\"suite\":\"s\",\"focus\":null,\"batch_size\":\"8\",\"recommended\":1,\"planned_total\":1,\"count_rule\":\"proportional\",\"behaviors\":[],\"sections\":{},\"batches\":[],\"notes\":[]}")]
    public void AnUnreadablePlan_IsRejected(string json)
    {
        Assert.Throws<GenerationPlanFormatException>(() => GenerationPlan.Parse(json));
    }
    // ---- Spec 095 (FR-006, FR-010; R5): only uncovered behaviours are batched ----

    private static ResolvedBehavior CoveredAt(string doc, int position, string title, string anchor, string coveredBy) =>
        new(title, "happy_path", "EP", $"{doc}#{anchor}", $"{doc}#{anchor}", doc, position, null, coveredBy);

    private static GenerationPlan BuildAsIngest(IReadOnlyList<ResolvedBehavior> behaviors, int batchSize = 8) =>
        GenerationPlanBuilder.Build("storage", null, batchSize, behaviors.Count(b => b.CoveredBy is null), behaviors,
            behaviors.Where(b => b.Ref is not null).Select(b => b.Ref!).Distinct()
                .ToDictionary(r => r, r => "sha256:" + new string('0', 64)),
            [], coverageThreshold: 0.6);

    [Fact]
    public void CoveredBehaviours_KeepTheirIdsAndCoveredBy_ButAreInNoBatch()
    {
        var plan = BuildAsIngest(
        [
            CoveredAt("docs/a.md", 1, "covered", "x", "TC-101"),
            At("docs/a.md", 1, "open", "x"),
            At("docs/b.md", 1, "other", "y"),
        ]);

        Assert.Equal(["B1", "B2", "B3"], plan.Behaviors.Select(b => b.Id));
        Assert.Equal("TC-101", plan.Behaviors[0].CoveredBy);
        Assert.Null(plan.Behaviors[1].CoveredBy);
        Assert.DoesNotContain(plan.Batches, b => b.BehaviorIds.Contains("B1"));
        Assert.Equal(["B2", "B3"], plan.Batches.SelectMany(b => b.BehaviorIds).Order());
    }

    [Fact]
    public void ACoveredUnresolvedBehaviour_IsNotInTheUnsourcedBatch()
    {
        var plan = BuildAsIngest(
        [
            new ResolvedBehavior("covered", "negative", "EG", "docs/nope.md", null, null, 0, "not found", "TC-101"),
            Unresolved("open", "docs/nope.md", "not found"),
        ]);

        var unsourced = Assert.Single(plan.Batches);
        Assert.Equal(GenerationPlan.Unsourced, unsourced.BatchId);
        Assert.Equal(["B2"], unsourced.BehaviorIds);
    }

    [Fact]
    public void EveryBatchCount_EqualsItsBehaviours_AndTheSumIsRecommended()
    {
        var behaviors = new List<ResolvedBehavior>();
        for (var d = 0; d < 4; d++)
        {
            behaviors.AddRange(Enumerable.Range(0, 3 + d * 2).Select(i => At($"docs/d{d}.md", i % 3, $"t{d}-{i}", $"s{i % 3}")));
        }

        behaviors.Add(CoveredAt("docs/d0.md", 1, "covered", "s1", "TC-101"));
        behaviors.Add(Unresolved("u", "docs/nope.md", "not found"));
        var plan = BuildAsIngest(behaviors, batchSize: 4);

        Assert.All(plan.Batches, b => Assert.Equal(b.BehaviorIds.Count, b.Count));
        Assert.Equal(plan.Recommended, plan.Batches.Sum(b => b.Count));
        Assert.Equal(plan.Recommended, plan.PlannedTotal);
        Assert.Equal(GenerationPlan.Proportional, plan.CountRule);
    }

    [Fact]
    public void AllCovered_GivesNoBatches()
    {
        var plan = BuildAsIngest([CoveredAt("docs/a.md", 1, "c1", "x", "TC-101"), CoveredAt("docs/a.md", 2, "c2", "y", "TC-102")]);

        Assert.Empty(plan.Batches);
        Assert.Equal(0, plan.PlannedTotal);
        Assert.Equal(0, plan.Recommended);
    }

    [Fact]
    public void Sections_ListOnlyTheRefsBatchesUse()
    {
        var plan = BuildAsIngest([CoveredAt("docs/a.md", 1, "covered", "only-covered", "TC-101"), At("docs/a.md", 2, "open", "used")]);

        Assert.Equal(["docs/a.md#used"], plan.Sections.Keys);
    }

    [Fact]
    public void TheThreshold_IsSerialisedAfterTheBatchSize_ByteIdentically()
    {
        var behaviors = new[] { CoveredAt("docs/a.md", 1, "covered", "x", "TC-101"), At("docs/a.md", 2, "open", "y") };
        var first = GenerationPlan.Serialize(BuildAsIngest(behaviors));

        Assert.Equal(first, GenerationPlan.Serialize(BuildAsIngest(behaviors)));
        Assert.True(first.IndexOf("\"batch_size\"", StringComparison.Ordinal) < first.IndexOf("\"coverage_threshold\": 0.6", StringComparison.Ordinal)); // Spec 098 F9: renamed key
        Assert.Contains("\"covered_by\": \"TC-101\"", first);
        Assert.Contains("\"covered_by\": null", first);
        var parsed = GenerationPlan.Parse(first);
        Assert.True(parsed.CoverageKnown);
        Assert.Equal(first, GenerationPlan.Serialize(parsed));
    }

    [Fact]
    public void A094Plan_Parses_WithCoverageUnknown_AndNoCoveredBehaviour()
    {
        var plan = GenerationPlan.Parse(GenerationPlanWorkspace.Plan094);

        Assert.False(plan.CoverageKnown);
        Assert.All(plan.Behaviors, b => Assert.Null(b.CoveredBy));
        Assert.Equal(2, plan.Batches.Count);
    }
}
