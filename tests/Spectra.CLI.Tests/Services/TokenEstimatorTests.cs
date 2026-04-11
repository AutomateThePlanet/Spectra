using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Services;

public class TokenEstimatorTests
{
    [Fact]
    public void Estimate_Null_ReturnsZero()
    {
        Assert.Equal(0, TokenEstimator.Estimate(null));
    }

    [Fact]
    public void Estimate_Empty_ReturnsZero()
    {
        Assert.Equal(0, TokenEstimator.Estimate(""));
    }

    [Theory]
    [InlineData("abcd", 1)]          // exactly 4 chars → 1 token
    [InlineData("abc", 0)]           // 3 chars → 0 (truncation)
    [InlineData("abcdefgh", 2)]      // 8 chars → 2
    [InlineData("abcdefghij", 2)]    // 10 chars → 2 (truncation)
    public void Estimate_ShortText_DividesByFour(string text, int expected)
    {
        Assert.Equal(expected, TokenEstimator.Estimate(text));
    }

    [Fact]
    public void Estimate_LongText_DividesByFour()
    {
        var text = new string('x', 4000);
        Assert.Equal(1000, TokenEstimator.Estimate(text));
    }

    [Fact]
    public void Estimate_MultilineText_CountsNewlines()
    {
        // "line1\nline2" = 11 characters → 2 tokens
        Assert.Equal(2, TokenEstimator.Estimate("line1\nline2"));
    }
}
