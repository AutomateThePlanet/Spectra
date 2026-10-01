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

    // ---------- Spec 081 C3: more doc markup, and bare boolean attributes ----------

    [Theory]
    [InlineData("<figure>x</figure>", "x")]
    [InlineData("<figcaption>x</figcaption>", "x")]
    [InlineData("<details><summary>x</summary>y</details>", "x y")]
    [InlineData("<section>x</section>", "x")]
    [InlineData("<article>x</article>", "x")]
    [InlineData("<aside>x</aside>", "x")]
    [InlineData("<table><caption>x</caption></table>", "x")]
    [InlineData("<colgroup><col span=2></colgroup>x", "x")]
    [InlineData("<tfoot>x</tfoot>", "x")]
    [InlineData("<center>x</center>", "x")]
    [InlineData("<font color=red>x</font>", "x")]
    [InlineData("<picture><source srcset=\"a.webp\">x</picture>", "x")]
    [InlineData("<big>x</big> <tt>y</tt>", "x y")]
    [InlineData("long<wbr>word", "long word")]
    [InlineData("<samp>x</samp> <dfn>y</dfn>", "x y")]
    [InlineData("<bdi>x</bdi> <bdo dir=rtl>y</bdo>", "x y")]
    [InlineData("<ruby>x<rp>(</rp><rt>y</rt><rp>)</rp></ruby>", "x ( y )")]
    [InlineData("<video src=\"a.mp4\">x</video>", "x")]
    [InlineData("<audio src=a.mp3>x</audio>", "x")]
    [InlineData("<iframe src=\"x.html\"></iframe>x", "x")]
    [InlineData("<INPUT type=checkbox>x", "x")]
    public void AddedElements_AreStripped(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("<td nowrap>cell</td>", "cell")]
    [InlineData("<details open><summary>s</summary>body</details>", "s body")]
    [InlineData("<input disabled> x", "x")]
    [InlineData("<video controls autoplay muted src=\"x\">v</video>", "v")]
    [InlineData("<td align=center nowrap>c</td>", "c")]
    [InlineData("<td nowrap align='left'>c</td>", "c")]
    [InlineData("<input type=checkbox checked/> x", "x")]
    [InlineData("<ol reversed>x</ol>", "x")]
    public void BooleanAttributes_AreAccepted(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("see <b openx> here", "see <b openx> here")]
    [InlineData("if a<b and c>d then", "if a<b and c>d then")]
    [InlineData("send <data> now", "send <data> now")]
    [InlineData("the <time> field", "the <time> field")]
    [InlineData("the <label> text", "the <label> text")]
    [InlineData("the <header> row", "the <header> row")]
    [InlineData("the <main> screen", "the <main> screen")]
    [InlineData("press <button>", "press <button>")]
    [InlineData("total<limit and qty>5", "total<limit and qty>5")]
    public void StillText(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    // ---------- Spec 082 A2: only HTML-form tags are markup; capitalised placeholders are text ----------

    [Theory]
    [InlineData("<Source>")]
    [InlineData("<Summary>")]
    [InlineData("<Section>")]
    [InlineData("<Input>")]
    [InlineData("<Details>")]
    [InlineData("<Caption>")]
    [InlineData("<LIMIT>")]
    [InlineData("<BR>")]
    public void CapitalisedPlaceholders_AreText(string placeholder)
    {
        Assert.Equal($"enter {placeholder.ToLowerInvariant()} now", TextNormalizer.Normalize($"Enter {placeholder} now"));
    }

    [Fact]
    public void PlaceholderSwaps_AreChanges()
    {
        Assert.NotEqual(TextNormalizer.Normalize("Enter <Summary> and press Save"),
            TextNormalizer.Normalize("Enter <Details> and press Save"));
        Assert.NotEqual(TextNormalizer.Normalize("Select <Source> from the list"),
            TextNormalizer.Normalize("Select <Target> from the list"));
    }

    [Theory]
    [InlineData("<details open>x</details>", "x")]
    [InlineData("x</Summary>", "x")]
    [InlineData("<Details open>x", "x")]
    [InlineData("<P>Para</P>", "para")]
    [InlineData("<Summary>x</Summary>", "x")]
    [InlineData("<figure><figcaption>c</figcaption></figure>", "c")]
    [InlineData("<INPUT type=checkbox>x", "x")]
    [InlineData("<Span class=\"k\">v</Span>", "v")]
    public void HtmlForm_IsStillMarkup(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    // ---------- Spec 083 F4: when an angle-bracket word counts as HTML (decision table, FR-012) ----------
    // Markup only as: a closing tag; a tag with attributes; a self-closing bare tag; lowercase <br>/<hr>/<wbr>; or a bare
    // opening tag closed later with EXACTLY the same spelling. Everything else is text — a missed change is worse than a
    // spurious one. Residual (documented): a lowercase placeholder whose exact lowercase close follows is markup.
    [Theory]
    [InlineData("x</Summary>", "x")]
    [InlineData("<details open>x", "x")]
    [InlineData("<Details open>x", "x")]
    [InlineData("<source srcset=\"a.webp\">x", "x")]
    [InlineData("<col span=2>x", "x")]
    [InlineData("a<br/>b", "a b")]
    [InlineData("a<hr />b", "a b")]
    [InlineData("a<BR/>b", "a b")]
    [InlineData("a<br>b", "a b")]
    [InlineData("a<hr>b", "a b")]
    [InlineData("a<wbr>b", "a b")]
    [InlineData("<p>x</p>", "x")]
    [InlineData("<P>x</P>", "x")]
    [InlineData("<details><summary>x</summary></details>", "x")]
    [InlineData("Enter <summary> x </summary>", "enter x")]
    [InlineData("send <source> now", "send <source> now")]
    [InlineData("<input> field", "<input> field")]
    [InlineData("the <summary> text", "the <summary> text")]
    [InlineData("the <section> box", "the <section> box")]
    [InlineData("<Summary>", "<summary>")]
    [InlineData("<Source>", "<source>")]
    [InlineData("<LIMIT>", "<limit>")]
    [InlineData("<BR>", "<br>")]
    [InlineData("<Summary> and </summary>", "<summary> and")]
    public void HtmlForms_DecisionTable(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
