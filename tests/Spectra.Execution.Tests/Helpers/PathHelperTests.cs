using System.IO;

namespace Spectra.MCP.Tests.Helpers;

/// <summary>
/// Tests for the path handling logic used when loading test cases.
/// This tests the fix for the doubled suite path issue (citizen/citizen/TC-100.md).
/// </summary>
public class PathHelperTests
{
    [Theory]
    [InlineData("citizen", "TC-100.md")]
    [InlineData("auth", "TC-001.md")]
    public void StripSuitePrefix_RemovesDuplicateSuite_ForwardSlash(string suite, string fileName)
    {
        var filePath = suite + "/" + fileName;
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(fileName, result);
    }

    [Theory]
    [InlineData("citizen", "TC-100.md")]
    [InlineData("auth", "TC-001.md")]
    public void StripSuitePrefix_RemovesDuplicateSuite_NativeSeparator(string suite, string fileName)
    {
        var filePath = suite + Path.DirectorySeparatorChar + fileName;
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(fileName, result);
    }

    [Theory]
    [InlineData("citizen", "TC-100.md")]
    [InlineData("auth", "TC-001.md")]
    public void StripSuitePrefix_PreservesPathsWithoutSuitePrefix(string suite, string filePath)
    {
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(filePath, result);
    }

    [Theory]
    [InlineData("citizen", "citizenx", "TC-100.md")]
    [InlineData("auth", "authentication", "TC-001.md")]
    public void StripSuitePrefix_DoesNotMatchPartialSuiteName(string suite, string otherDir, string fileName)
    {
        var filePath = otherDir + Path.DirectorySeparatorChar + fileName;
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(filePath, result);
    }

    [Fact]
    public void BuildTestPath_WithOldIndexFormat_ProducesCorrectPath()
    {
        var basePath = Path.Combine("project");
        var suite = "citizen";
        var oldFormatFile = "citizen" + Path.DirectorySeparatorChar + "TC-100.md";

        var fileName = StripSuitePrefixIfPresent(suite, oldFormatFile);
        var testPath = Path.Combine(basePath, "test-cases", suite, fileName);

        Assert.Equal(Path.Combine("project", "test-cases", "citizen", "TC-100.md"), testPath);
    }

    [Fact]
    public void BuildTestPath_WithNewIndexFormat_ProducesCorrectPath()
    {
        var basePath = Path.Combine("project");
        var suite = "citizen";
        var newFormatFile = "TC-100.md";

        var fileName = StripSuitePrefixIfPresent(suite, newFormatFile);
        var testPath = Path.Combine(basePath, "test-cases", suite, fileName);

        Assert.Equal(Path.Combine("project", "test-cases", "citizen", "TC-100.md"), testPath);
    }

    /// <summary>
    /// Helper method that mirrors the logic in Program.cs testCaseLoader.
    /// </summary>
    private static string StripSuitePrefixIfPresent(string suite, string filePath)
    {
        if (filePath.StartsWith(suite + Path.DirectorySeparatorChar) ||
            filePath.StartsWith(suite + Path.AltDirectorySeparatorChar))
        {
            return filePath.Substring(suite.Length + 1);
        }
        return filePath;
    }
}
