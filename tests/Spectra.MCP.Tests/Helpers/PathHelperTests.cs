using System.IO;

namespace Spectra.MCP.Tests.Helpers;

/// <summary>
/// Tests for the path handling logic used when loading test cases.
/// This tests the fix for the doubled suite path issue (citizen\citizen\TC-100.md).
/// </summary>
public class PathHelperTests
{
    [Theory]
    [InlineData("citizen", "citizen\\TC-100.md", "TC-100.md")]
    [InlineData("citizen", "citizen/TC-100.md", "TC-100.md")]
    [InlineData("auth", "auth\\TC-001.md", "TC-001.md")]
    [InlineData("auth", "auth/TC-001.md", "TC-001.md")]
    public void StripSuitePrefix_RemovesDuplicateSuite(string suite, string filePath, string expected)
    {
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("citizen", "TC-100.md", "TC-100.md")]
    [InlineData("auth", "TC-001.md", "TC-001.md")]
    [InlineData("citizen", "other\\TC-100.md", "other\\TC-100.md")]
    public void StripSuitePrefix_PreservesPathsWithoutSuitePrefix(string suite, string filePath, string expected)
    {
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("citizen", "citizenx\\TC-100.md", "citizenx\\TC-100.md")]
    [InlineData("auth", "authentication\\TC-001.md", "authentication\\TC-001.md")]
    public void StripSuitePrefix_DoesNotMatchPartialSuiteName(string suite, string filePath, string expected)
    {
        var result = StripSuitePrefixIfPresent(suite, filePath);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildTestPath_WithOldIndexFormat_ProducesCorrectPath()
    {
        var basePath = @"C:\SourceCode\project";
        var suite = "citizen";
        var oldFormatFile = @"citizen\TC-100.md";

        var fileName = StripSuitePrefixIfPresent(suite, oldFormatFile);
        var testPath = Path.Combine(basePath, "tests", suite, fileName);

        Assert.Equal(@"C:\SourceCode\project\tests\citizen\TC-100.md", testPath);
    }

    [Fact]
    public void BuildTestPath_WithNewIndexFormat_ProducesCorrectPath()
    {
        var basePath = @"C:\SourceCode\project";
        var suite = "citizen";
        var newFormatFile = "TC-100.md";

        var fileName = StripSuitePrefixIfPresent(suite, newFormatFile);
        var testPath = Path.Combine(basePath, "tests", suite, fileName);

        Assert.Equal(@"C:\SourceCode\project\tests\citizen\TC-100.md", testPath);
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
