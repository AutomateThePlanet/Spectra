using System.Globalization;
using Spectra.Core.Text;

namespace Spectra.Core.Tests.Text;

/// <summary>Spec 079 FR-013: the shared comparison-key normaliser.</summary>
public class TextNormalizerTests
{
    [Theory]
    [InlineData("System **MUST** validate", "system must validate")]
    [InlineData("System __MUST__ validate", "system must validate")]
    [InlineData("System *must* validate", "system must validate")]
    [InlineData("System _must_ validate", "system must validate")]
    [InlineData("Old ~~limit~~ removed", "old limit removed")]
    [InlineData("Call `validate()` first", "call validate() first")]
    [InlineData("See [the spec](https://x.y/spec) now", "see the spec now")]
    [InlineData("See [the spec][ref] now", "see the spec now")]
    [InlineData("Logo ![company logo](logo.png) shown", "logo company logo shown")]
    [InlineData("Docs at <https://x.y/docs>", "docs at https://x.y/docs")]
    [InlineData("A <b>bold</b> move<br/>next", "a bold move next")]
    [InlineData("Fish &amp; chips&nbsp;now", "fish & chips now")]
    [InlineData("  many   spaces\tand\nlines  ", "many spaces and lines")]
    [InlineData("UPPER Case", "upper case")]
    public void Normalize_StripsMarkupWhitespaceAndCase(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("set max_retry_count to 3", "set max_retry_count to 3")]
    [InlineData("result is 2 * 3", "result is 2 * 3")]
    [InlineData("a_b_c stays", "a_b_c stays")]
    [InlineData("glob file*.md here", "glob file*.md here")]
    [InlineData("if a < b and c > d", "if a < b and c > d")]
    public void Normalize_LeavesNonMarkupAlone(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankIsEmpty(string? input)
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("System **MUST** see [x](y) &amp; `z`")]
    [InlineData("plain text")]
    public void Normalize_IsIdempotent(string input)
    {
        var once = TextNormalizer.Normalize(input);

        Assert.Equal(once, TextNormalizer.Normalize(once));
    }

    [Fact]
    public void Normalize_IsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal("identifier", TextNormalizer.Normalize("IDENTIFIER"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
