using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>Spec 084 T2 (FR-003, D2): tolerant anchor matching. One row per anchor style.</summary>
public class AnchorKeyTests
{
    private const string Heading = "Create customer records";

    [Theory]
    [InlineData("Create-customer-records")]      // GitBook
    [InlineData("create-customer-records")]      // GitHub
    [InlineData("Create-Customer-Records")]      // generator style
    [InlineData("create_customer_records")]      // underscores
    [InlineData("Create%20customer%20records")]  // percent-escaped
    [InlineData("create-customer-records!")]     // trailing punctuation
    [InlineData("CREATE.customer.records")]      // dots, case
    public void AnchorStyles_MatchTheHeading(string anchor)
    {
        Assert.Equal("createcustomerrecords", AnchorKey.FromAnchor(anchor));
        Assert.Equal(AnchorKey.Make(Heading), AnchorKey.FromAnchor(anchor));
    }

    [Fact]
    public void ADifferentWord_DoesNotMatch() =>
        Assert.NotEqual(AnchorKey.Make(Heading), AnchorKey.FromAnchor("create-customer-record"));

    [Theory]
    [InlineData("Über Uns", "überuns")]
    [InlineData("Step 2: Save", "step2save")]
    [InlineData("C# & .NET", "cnet")]
    [InlineData("", "")]
    public void Make_KeepsLettersAndDigitsLowercased(string text, string expected) =>
        Assert.Equal(expected, AnchorKey.Make(text));

    [Fact]
    public void Diacritics_AreKept() => Assert.NotEqual(AnchorKey.Make("Über"), AnchorKey.FromAnchor("uber"));

    [Fact]
    public void MalformedEscape_IsKeptRaw_WithoutThrowing() =>
        Assert.Equal("zzab", AnchorKey.FromAnchor("%zz-ab"));
}
