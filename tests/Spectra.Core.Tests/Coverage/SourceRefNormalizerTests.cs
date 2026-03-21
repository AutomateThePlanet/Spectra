using Spectra.Core.Coverage;

namespace Spectra.Core.Tests.Coverage;

public class SourceRefNormalizerTests
{
    [Theory]
    [InlineData("docs/auth.md#Login-Flow", "docs/auth.md")]
    [InlineData("docs/checkout.md#Payment-Flow", "docs/checkout.md")]
    [InlineData("docs/auth.md#section#subsection", "docs/auth.md")]
    [InlineData("docs/auth.md", "docs/auth.md")]
    [InlineData("", "")]
    public void StripFragment_RemovesEverythingAfterHash(string input, string expected)
    {
        Assert.Equal(expected, SourceRefNormalizer.StripFragment(input));
    }

    [Fact]
    public void StripFragment_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SourceRefNormalizer.StripFragment(null!));
    }

    [Theory]
    [InlineData("docs/auth.md#Login-Flow", "docs/auth.md")]
    [InlineData("docs\\auth.md", "docs/auth.md")]
    [InlineData("/docs/auth.md", "docs/auth.md")]
    [InlineData("\\docs\\auth.md#Section", "docs/auth.md")]
    [InlineData("docs/auth.md", "docs/auth.md")]
    [InlineData("", "")]
    public void NormalizePath_StripsFragmentAndNormalizesSlashes(string input, string expected)
    {
        Assert.Equal(expected, SourceRefNormalizer.NormalizePath(input));
    }

    [Fact]
    public void NormalizePath_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SourceRefNormalizer.NormalizePath(null!));
    }

    [Theory]
    [InlineData("docs/Auth.md#Login-Flow", "docs/auth.md")]
    [InlineData("DOCS\\AUTH.MD", "docs/auth.md")]
    [InlineData("/Docs/Checkout.md#Payment", "docs/checkout.md")]
    [InlineData("docs/auth.md", "docs/auth.md")]
    public void NormalizeForComparison_LowercasesAndNormalizes(string input, string expected)
    {
        Assert.Equal(expected, SourceRefNormalizer.NormalizeForComparison(input));
    }

    [Fact]
    public void NormalizeForComparison_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SourceRefNormalizer.NormalizeForComparison(null!));
    }

    [Fact]
    public void NormalizePath_PreservesRelativePaths()
    {
        Assert.Equal("docs/subfolder/file.md", SourceRefNormalizer.NormalizePath("docs/subfolder/file.md"));
    }

    [Fact]
    public void NormalizePath_HandlesMultipleBackslashes()
    {
        Assert.Equal("docs/sub/file.md", SourceRefNormalizer.NormalizePath("docs\\sub\\file.md"));
    }
}
