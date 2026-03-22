using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class FrontmatterUpdaterBugsTests
{
    private readonly FrontmatterUpdater _updater = new();

    private const string BasicFrontmatter = """
        ---
        id: TC-101
        priority: high
        tags:
          - login
        ---
        # Test Case
        """;

    [Fact]
    public void UpdateBugs_InsertsSingleBug()
    {
        var result = _updater.UpdateBugs(BasicFrontmatter, ["BUG-42"]);

        Assert.NotNull(result);
        Assert.Contains("bugs:", result);
        Assert.Contains("  - BUG-42", result);
    }

    [Fact]
    public void UpdateBugs_InsertsMultipleBugs()
    {
        var result = _updater.UpdateBugs(BasicFrontmatter, ["BUG-42", "https://github.com/org/repo/issues/99"]);

        Assert.NotNull(result);
        Assert.Contains("  - BUG-42", result);
        Assert.Contains("  - https://github.com/org/repo/issues/99", result);
    }

    [Fact]
    public void UpdateBugs_EmptyList_SetsEmptyArray()
    {
        var result = _updater.UpdateBugs(BasicFrontmatter, []);

        Assert.NotNull(result);
        Assert.Contains("bugs: []", result);
    }

    [Fact]
    public void UpdateBugs_ReplacesExistingInline()
    {
        var content = """
            ---
            id: TC-101
            bugs: []
            ---
            # Test
            """;

        var result = _updater.UpdateBugs(content, ["BUG-99"]);

        Assert.NotNull(result);
        Assert.Contains("  - BUG-99", result);
        Assert.DoesNotContain("bugs: []", result);
    }

    [Fact]
    public void UpdateBugs_ReplacesExistingBlock()
    {
        var content = "---\nid: TC-101\nbugs:\n  - BUG-old\npriority: high\n---\n# Test\n";

        var result = _updater.UpdateBugs(content, ["BUG-new"]);

        Assert.NotNull(result);
        Assert.Contains("  - BUG-new", result);
        Assert.DoesNotContain("BUG-old", result);
    }

    [Fact]
    public void UpdateBugs_ReturnsNull_WhenNoFrontmatter()
    {
        var content = "# No frontmatter here\nJust content.";

        var result = _updater.UpdateBugs(content, ["BUG-1"]);

        Assert.Null(result);
    }

    [Fact]
    public void UpdateBugs_PreservesOtherFields()
    {
        var result = _updater.UpdateBugs(BasicFrontmatter, ["BUG-42"]);

        Assert.NotNull(result);
        Assert.Contains("id: TC-101", result);
        Assert.Contains("priority: high", result);
        Assert.Contains("  - login", result);
        Assert.Contains("# Test Case", result);
    }
}
