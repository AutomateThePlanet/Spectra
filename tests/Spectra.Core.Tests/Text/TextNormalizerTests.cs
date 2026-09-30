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

    // ---------- Spec 080 B9: only real HTML tags are stripped ----------

    [Fact]
    public void Comparisons_AreNotTags_SoDifferentCriteriaStayDifferent()
    {
        var five = TextNormalizer.Normalize("total<limit and qty>5");
        var six = TextNormalizer.Normalize("total<limit and qty>6");

        Assert.NotEqual(five, six);
        Assert.Contains("limit and qty", five);
        Assert.Equal("total<limit and qty>5", five);
    }

    [Theory]
    [InlineData("a<b", "a<b")]
    [InlineData("x > 3 and y < 2", "x > 3 and y < 2")]
    [InlineData("if a<b and c>d then", "if a<b and c>d then")]
    [InlineData("value <Limit> applies", "value <limit> applies")]
    [InlineData("use <placeholder> here", "use <placeholder> here")]
    public void AngleBrackets_ThatAreNotHtmlTags_AreKept(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("<b>x</b>", "x")]
    [InlineData("<b>a < b</b>", "a < b")]
    [InlineData("line<br/>next", "line next")]
    [InlineData("line<br>next", "line next")]
    [InlineData("<span class=\"k\">v</span>", "v")]
    [InlineData("<a href='x.html' title=\"T\">link</a>", "link")]
    [InlineData("before <!-- note --> after", "before after")]
    [InlineData("<P>Para</P>", "para")]
    [InlineData("<td align=center>cell</td>", "cell")]
    public void RealHtmlTags_AreStripped(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
