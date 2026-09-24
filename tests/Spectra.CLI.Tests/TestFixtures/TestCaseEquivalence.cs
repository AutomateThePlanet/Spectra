using System.Collections;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Deep comparison of two <see cref="TestCase"/> values for round-trip tests. The record's synthesized
/// equality compares list members by reference, so it cannot be used for this.
/// </summary>
public static class TestCaseEquivalence
{
    public static void AssertEquivalent(TestCase expected, TestCase actual, bool includeLayout = true)
    {
        var diff = FirstDifference(expected, actual, includeLayout);
        Assert.True(diff is null, $"TestCase differs at: {diff}");
    }

    public static string? FirstDifference(TestCase e, TestCase a, bool includeLayout = true)
    {
        if (e.Id != a.Id) return nameof(TestCase.Id);
        if (e.Priority != a.Priority) return nameof(TestCase.Priority);
        if (!e.Tags.SequenceEqual(a.Tags)) return nameof(TestCase.Tags);
        if (e.Component != a.Component) return nameof(TestCase.Component);
        if (e.Description != a.Description) return nameof(TestCase.Description);
        if (e.Preconditions != a.Preconditions) return nameof(TestCase.Preconditions);
        if (!e.Environment.SequenceEqual(a.Environment)) return nameof(TestCase.Environment);
        if (e.EstimatedDuration != a.EstimatedDuration) return nameof(TestCase.EstimatedDuration);
        if (e.DependsOn != a.DependsOn) return nameof(TestCase.DependsOn);
        if (!e.SourceRefs.SequenceEqual(a.SourceRefs)) return nameof(TestCase.SourceRefs);
        if (e.ScenarioFromDoc != a.ScenarioFromDoc) return nameof(TestCase.ScenarioFromDoc);
        if (!e.RelatedWorkItems.SequenceEqual(a.RelatedWorkItems)) return nameof(TestCase.RelatedWorkItems);
        if (!DeepEqual(e.Custom, a.Custom)) return nameof(TestCase.Custom);
        if (!e.AutomatedBy.SequenceEqual(a.AutomatedBy)) return nameof(TestCase.AutomatedBy);
        if (!e.Requirements.SequenceEqual(a.Requirements)) return nameof(TestCase.Requirements);
        if (!e.Criteria.SequenceEqual(a.Criteria)) return nameof(TestCase.Criteria);
        if (!e.Bugs.SequenceEqual(a.Bugs)) return nameof(TestCase.Bugs);
        if (e.Status != a.Status) return nameof(TestCase.Status);
        if (e.OrphanedReason != a.OrphanedReason) return nameof(TestCase.OrphanedReason);
        if (e.OrphanedDate != a.OrphanedDate) return nameof(TestCase.OrphanedDate);
        if (e.Title != a.Title) return nameof(TestCase.Title);
        if (!e.Steps.SequenceEqual(a.Steps)) return nameof(TestCase.Steps);
        if (e.ExpectedResult != a.ExpectedResult) return nameof(TestCase.ExpectedResult);
        if (e.TestData != a.TestData) return nameof(TestCase.TestData);

        var grounding = GroundingDifference(e, a);
        if (grounding is not null) return grounding;

        if (!includeLayout) return null;
        if ((e.Layout is null) != (a.Layout is null)) return nameof(TestCase.Layout);
        if (e.Layout is null || a.Layout is null) return null;
        if (e.Layout.Newline != a.Layout.Newline) return "Layout.Newline";
        if (!e.Layout.FrontmatterEntries.SequenceEqual(a.Layout.FrontmatterEntries)) return "Layout.FrontmatterEntries";
        if (!e.Layout.BodySegments.SequenceEqual(a.Layout.BodySegments)) return "Layout.BodySegments";
        return null;
    }

    private static string? GroundingDifference(TestCase e, TestCase a)
    {
        var ge = e.Grounding;
        var ga = a.Grounding;
        if (ge is null && ga is null) return null;
        if (ge is null || ga is null) return "Grounding";
        if (ge.Verdict != ga.Verdict) return "Grounding.Verdict";
        if (ge.Score != ga.Score) return "Grounding.Score";
        if (ge.Generator != ga.Generator) return "Grounding.Generator";
        if (ge.Critic != ga.Critic) return "Grounding.Critic";
        if (ge.VerifiedAt != ga.VerifiedAt) return "Grounding.VerifiedAt";
        if (!ge.UnverifiedClaims.SequenceEqual(ga.UnverifiedClaims)) return "Grounding.UnverifiedClaims";
        if (ge.FlaggedForReview != ga.FlaggedForReview) return "Grounding.FlaggedForReview";
        if (ge.RepairAttempts != ga.RepairAttempts) return "Grounding.RepairAttempts";
        if (ge.Repaired != ga.Repaired) return "Grounding.Repaired";
        if (!ge.CondensedFindings.SequenceEqual(ga.CondensedFindings)) return "Grounding.CondensedFindings";
        if (ge.Source != ga.Source) return "Grounding.Source";
        if (ge.CreatedBy != ga.CreatedBy) return "Grounding.CreatedBy";
        if (ge.Note != ga.Note) return "Grounding.Note";
        return null;
    }

    private static bool DeepEqual(object? x, object? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x is string sx) return y is string sy && sx == sy;

        if (x is IDictionary dx && y is IDictionary dy)
        {
            if (dx.Count != dy.Count) return false;
            var xs = Entries(dx);
            var ys = Entries(dy);
            return xs.Zip(ys).All(p => DeepEqual(p.First.Key, p.Second.Key) && DeepEqual(p.First.Value, p.Second.Value));
        }

        if (x is IEnumerable ex && y is IEnumerable ey)
        {
            var xl = ex.Cast<object?>().ToList();
            var yl = ey.Cast<object?>().ToList();
            return xl.Count == yl.Count && xl.Zip(yl).All(p => DeepEqual(p.First, p.Second));
        }

        return Equals(x, y);
    }

    private static List<(object Key, object? Value)> Entries(IDictionary map)
    {
        var entries = new List<(object, object?)>();
        var enumerator = map.GetEnumerator();
        while (enumerator.MoveNext())
        {
            entries.Add((enumerator.Key, enumerator.Value));
        }

        return entries;
    }
}
