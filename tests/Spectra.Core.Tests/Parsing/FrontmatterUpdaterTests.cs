using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class FrontmatterUpdaterTests
{
    private readonly FrontmatterUpdater _updater = new();

    [Fact]
    public void UpdateAutomatedBy_NoFrontmatter_ReturnsNull()
    {
        var content = "# Just a heading\nNo frontmatter here.";

        var result = _updater.UpdateAutomatedBy(content, ["tests/Login.cs"]);

        Assert.Null(result);
    }

    [Fact]
    public void UpdateAutomatedBy_AddsNewField()
    {
        var content = """
            ---
            id: TC-001
            priority: high
            ---
            # Test Title
            """;

        var result = _updater.UpdateAutomatedBy(content, ["tests/LoginTests.cs"]);

        Assert.NotNull(result);
        Assert.Contains("automated_by:", result);
        Assert.Contains("  - tests/LoginTests.cs", result);
        Assert.Contains("id: TC-001", result);
        Assert.Contains("priority: high", result);
    }

    [Fact]
    public void UpdateAutomatedBy_ReplacesExistingInline()
    {
        var content = """
            ---
            id: TC-001
            automated_by: old_value
            priority: high
            ---
            # Test Title
            """;

        var result = _updater.UpdateAutomatedBy(content, ["tests/NewFile.cs"]);

        Assert.NotNull(result);
        Assert.Contains("  - tests/NewFile.cs", result);
        Assert.DoesNotContain("old_value", result);
    }

    [Fact]
    public void UpdateAutomatedBy_MultipleFiles()
    {
        var content = """
            ---
            id: TC-001
            ---
            # Test Title
            """;

        var result = _updater.UpdateAutomatedBy(content, ["tests/A.cs", "tests/B.cs"]);

        Assert.NotNull(result);
        Assert.Contains("  - tests/A.cs", result);
        Assert.Contains("  - tests/B.cs", result);
    }

    [Fact]
    public void UpdateAutomatedBy_EmptyList_WritesEmptyArray()
    {
        var content = """
            ---
            id: TC-001
            ---
            # Test Title
            """;

        var result = _updater.UpdateAutomatedBy(content, []);

        Assert.NotNull(result);
        Assert.Contains("automated_by: []", result);
    }

    [Fact]
    public void UpdateAutomatedBy_PreservesOtherFields()
    {
        var content = """
            ---
            id: TC-001
            priority: high
            tags:
              - smoke
              - regression
            component: auth
            ---
            # Test Title
            """;

        var result = _updater.UpdateAutomatedBy(content, ["tests/Test.cs"]);

        Assert.NotNull(result);
        Assert.Contains("id: TC-001", result);
        Assert.Contains("priority: high", result);
        Assert.Contains("component: auth", result);
        Assert.Contains("- smoke", result);
    }

    [Fact]
    public void UpdateAutomatedBy_PreservesBodyContent()
    {
        var content = """
            ---
            id: TC-001
            ---
            # Test Title

            ## Steps
            1. Do something
            2. Check result

            ## Expected Result
            Something happens
            """;

        var result = _updater.UpdateAutomatedBy(content, ["tests/Test.cs"]);

        Assert.NotNull(result);
        Assert.Contains("# Test Title", result);
        Assert.Contains("## Steps", result);
        Assert.Contains("1. Do something", result);
    }

    [Fact]
    public async Task UpdateFileAsync_NonexistentFile_ReturnsFalse()
    {
        var result = await _updater.UpdateFileAsync("/nonexistent/file.md", ["test.cs"]);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateFileAsync_WritesUpdatedContent()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                ---
                id: TC-001
                ---
                # Test
                """);

            var result = await _updater.UpdateFileAsync(tempFile, ["tests/Test.cs"]);

            Assert.True(result);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("automated_by:", content);
            Assert.Contains("  - tests/Test.cs", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
