using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Infrastructure;

public class PathSanitizerTests
{
    [Theory]
    [InlineData("valid-name")]
    [InlineData("my_suite")]
    [InlineData("test123")]
    [InlineData("CamelCase")]
    public void IsValidSegment_ValidNames_ReturnsTrue(string segment)
    {
        Assert.True(PathSanitizer.IsValidSegment(segment));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("..")]
    [InlineData("../parent")]
    [InlineData("./current")]
    [InlineData(".\\windows")]
    [InlineData("C:\\absolute")]
    [InlineData("/root/path")]
    [InlineData("name<invalid>")]
    [InlineData("name:colon")]
    [InlineData("name|pipe")]
    public void IsValidSegment_InvalidNames_ReturnsFalse(string? segment)
    {
        Assert.False(PathSanitizer.IsValidSegment(segment));
    }

    [Theory]
    [InlineData("../escape", "escape")]
    [InlineData("name<>chars", "name__chars")]
    [InlineData("  spaces  ", "spaces")]
    public void SanitizeSegment_InvalidChars_RemovesOrReplaces(string input, string expected)
    {
        var result = PathSanitizer.SanitizeSegment(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    public void SanitizeSegment_Unsanitizable_ReturnsNull(string? input)
    {
        Assert.Null(PathSanitizer.SanitizeSegment(input));
    }

    [Fact]
    public void IsPathWithinBase_ValidSubpath_ReturnsTrue()
    {
        var basePath = Path.GetTempPath();
        var subPath = Path.Combine(basePath, "subdir", "file.txt");

        Assert.True(PathSanitizer.IsPathWithinBase(basePath, subPath));
    }

    [Fact]
    public void IsPathWithinBase_PathTraversal_ReturnsFalse()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "base");
        var escapedPath = Path.Combine(basePath, "..", "escaped");

        Assert.False(PathSanitizer.IsPathWithinBase(basePath, escapedPath));
    }

    [Fact]
    public void SafeCombine_ValidSegments_ReturnsCombinedPath()
    {
        var basePath = Path.GetTempPath();
        var result = PathSanitizer.SafeCombine(basePath, "subdir", "file.txt");

        Assert.NotNull(result);
        Assert.StartsWith(basePath, result);
    }

    [Fact]
    public void SafeCombine_TraversalAttempt_ReturnsNull()
    {
        var basePath = Path.GetTempPath();
        var result = PathSanitizer.SafeCombine(basePath, "..", "escaped");

        Assert.Null(result);
    }

    [Fact]
    public void SafeCombine_InvalidSegment_ReturnsNull()
    {
        var basePath = Path.GetTempPath();
        var result = PathSanitizer.SafeCombine(basePath, "name:invalid");

        Assert.Null(result);
    }
}
