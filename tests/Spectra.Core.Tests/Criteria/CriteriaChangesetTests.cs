using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>Spec 079 FR-012: the per-ingest changeset.</summary>
public class CriteriaChangesetTests
{
    private static AcceptanceCriterion C(string id, string text, string priority = "medium",
        string? rfc = "MUST", string? section = "Payment", string? hint = null, params string[] tags) => new()
    {
        Id = id, Text = text, Priority = priority, Rfc2119 = rfc, SourceSection = section, TechniqueHint = hint,
        Tags = tags.ToList()
    };

    [Fact]
    public void Build_ReportsAddedModifiedRemovedAndMetadata()
    {
        var previous = new[]
        {
            C("AC-X-001", "System MUST validate"),
            C("AC-X-002", "System SHOULD warn", "medium", "SHOULD"),
            C("AC-X-003", "System MAY retry", "low", "MAY")
        };
        var next = new[]
        {
            C("AC-X-001", "System MUST validate the IBAN"),
            C("AC-X-002", "System SHOULD warn", "high", "SHOULD", "Warnings", "BVA", "ux"),
            C("AC-X-004", "System MUST log")
        };

        var cs = CriteriaChangeset.Build("docs/x.md", previous, next);

        Assert.Equal("AC-X-004", Assert.Single(cs.Added).Id);
        var modified = Assert.Single(cs.Modified);
        Assert.Equal(("AC-X-001", "System MUST validate", "System MUST validate the IBAN"),
            (modified.Id, modified.OldText, modified.NewText));
        var removed = Assert.Single(cs.Removed);
        Assert.Equal(("AC-X-003", "System MAY retry"), (removed.Id, removed.OldText));
        Assert.Equal(
            [("priority", "medium", "high"), ("source_section", "Payment", "Warnings"), ("tags", null, "ux"), ("technique_hint", null, "BVA")],
            cs.Metadata.Where(m => m.Id == "AC-X-002").Select(m => (m.Field, m.Old, m.New)));
        Assert.False(cs.IsEmpty);
    }

    [Fact]
    public void Build_IdenticalSetsInAnyOrder_IsEmpty()
    {
        var a = new[] { C("AC-X-001", "one"), C("AC-X-002", "two") };
        var b = new[] { C("AC-X-002", "two"), C("AC-X-001", "one") };

        Assert.True(CriteriaChangeset.Build("d", a, b).IsEmpty);
    }

    [Fact]
    public void Build_MarkupOrWhitespaceOnly_IsNotModified()
    {
        var cs = CriteriaChangeset.Build("d",
            [C("AC-X-001", "System **MUST** validate")],
            [C("AC-X-001", "System MUST   validate")]);

        Assert.True(cs.IsEmpty);
    }

    [Fact]
    public void Build_ListsAreSortedById()
    {
        var cs = CriteriaChangeset.Build("d", [], [C("AC-X-010", "b"), C("AC-X-002", "a")]);

        Assert.Equal(["AC-X-002", "AC-X-010"], cs.Added.Select(a => a.Id));
    }

    [Fact]
    public void WithReferences_FillsRemovedCounts()
    {
        var cs = CriteriaChangeset.Build("d", [C("AC-X-001", "a"), C("AC-X-002", "b")], [])
            .WithReferences(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ac-x-001"] = ["TC-101", "TC-140"]
            });

        Assert.Equal(2, cs.Removed[0].ReferencingTests);
        Assert.Equal(["TC-101", "TC-140"], cs.Removed[0].TestIds);
        Assert.Equal(0, cs.Removed[1].ReferencingTests);
    }

    // ---------- Spec 080 B4: tags compare as a case-insensitive set ----------

    [Fact]
    public void Tags_ReorderedOrRecased_AreNotAChange()
    {
        var previous = new[] { C("AC-X-001", "System MUST validate", tags: ["ux", "Login"]) };
        var next = new[] { C("AC-X-001", "System MUST validate", tags: ["login", "UX", "ux"]) };

        Assert.True(CriteriaChangeset.Build("docs/x.md", previous, next).IsEmpty);
    }

    [Fact]
    public void Tags_WithADifferentSet_AreAChange_ReportedWithTheirOriginalText()
    {
        var previous = new[] { C("AC-X-001", "System MUST validate", tags: ["a"]) };
        var next = new[] { C("AC-X-001", "System MUST validate", tags: ["a", "B"]) };

        var change = Assert.Single(CriteriaChangeset.Build("docs/x.md", previous, next).Metadata);

        Assert.Equal("tags", change.Field);
        Assert.Equal("a", change.Old);
        Assert.Equal("a, B", change.New);
    }

    // Spec 082 A2: a change inside a capitalised placeholder is a real change.
    [Fact]
    public void PlaceholderChange_IsAModification()
    {
        var previous = new[] { C("AC-X-001", "Enter <Summary> and press Save") };
        var next = new[] { C("AC-X-001", "Enter <Details> and press Save") };

        var modified = Assert.Single(CriteriaChangeset.Build("docs/x.md", previous, next).Modified);

        Assert.Equal("AC-X-001", modified.Id);
    }
}
