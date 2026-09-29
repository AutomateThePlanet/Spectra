using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>Spec 079 FR-004/FR-005: explicit-ID validation and the one-to-one text fallback.</summary>
public class CriteriaMatcherTests
{
    private static AcceptanceCriterion C(string text, string id = "") => new() { Id = id, Text = text };

    private static IncomingCriterion In(string text, string? requestedId = null) => new(C(text), requestedId);

    private static readonly IReadOnlyList<AcceptanceCriterion> Previous =
    [
        C("System MUST validate the card", "AC-CHECKOUT-001"),
        C("System SHOULD show an error", "AC-CHECKOUT-002"),
        C("System MAY retry once", "AC-CHECKOUT-003")
    ];

    [Fact]
    public void ExplicitValidId_IsKept_WithNewText()
    {
        var result = CriteriaMatcher.Match([In("System MUST reject expired cards", "AC-CHECKOUT-001")], Previous);

        Assert.True(result.IsValid);
        var item = Assert.Single(result.Items);
        Assert.Equal("AC-CHECKOUT-001", item.Criterion.Id);
        Assert.Equal("System MUST reject expired cards", item.Criterion.Text);
        Assert.Same(Previous[0], item.Previous);
    }

    [Theory]
    [InlineData("AC-LOGIN-001")]   // another doc's ID
    [InlineData("AC-CHECKOUT-999")] // unknown
    public void ForeignOrUnknownId_IsInvalid(string id)
    {
        var result = CriteriaMatcher.Match([In("x", id)], Previous);

        Assert.False(result.IsValid);
        Assert.Equal([id], result.InvalidIds);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void DuplicatedId_IsInvalid()
    {
        var result = CriteriaMatcher.Match(
            [In("a", "AC-CHECKOUT-001"), In("b", "ac-checkout-001")], Previous);

        Assert.False(result.IsValid);
        Assert.Single(result.InvalidIds);
    }

    [Fact]
    public void TextFallback_IsOneToOne()
    {
        var result = CriteriaMatcher.Match(
            [In("System **MUST** validate the card"), In("system must validate   the card")], Previous);

        Assert.True(result.IsValid);
        Assert.Equal("AC-CHECKOUT-001", result.Items[0].Criterion.Id);
        Assert.Equal(string.Empty, result.Items[1].Criterion.Id); // second copy is new
        Assert.Null(result.Items[1].Previous);
    }

    [Fact]
    public void ExplicitId_TakesPrecedenceOverATextMatch()
    {
        // Item 0 has the text of 001 but no ID; item 1 explicitly claims 001.
        var result = CriteriaMatcher.Match(
            [In("System MUST validate the card"), In("Card validation is mandatory", "AC-CHECKOUT-001")], Previous);

        Assert.Equal(string.Empty, result.Items[0].Criterion.Id);
        Assert.Equal("AC-CHECKOUT-001", result.Items[1].Criterion.Id);
    }

    [Fact]
    public void KeptIdWithEqualNormalisedText_KeepsThePreviousTextVerbatim()
    {
        var result = CriteriaMatcher.Match([In("System **should** show   an error", "AC-CHECKOUT-002")], Previous);

        Assert.Equal("System SHOULD show an error", result.Items[0].Criterion.Text);
    }

    [Fact]
    public void Unmatched_AreReturnedInResponseOrder_WithoutIds()
    {
        var result = CriteriaMatcher.Match([In("new one"), In("System MAY retry once"), In("new two")], Previous);

        Assert.Equal(["", "AC-CHECKOUT-003", ""], result.Items.Select(i => i.Criterion.Id));
        Assert.Equal(["new one", "System MAY retry once", "new two"], result.Items.Select(i => i.Criterion.Text));
    }
}
