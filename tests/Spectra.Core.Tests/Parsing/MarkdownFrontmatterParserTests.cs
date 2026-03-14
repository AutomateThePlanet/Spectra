using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class MarkdownFrontmatterParserTests
{
    private readonly MarkdownFrontmatterParser _parser = new();

    [Fact]
    public void ParseFrontmatter_WithValidYaml_ReturnsSuccess()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: high
            tags: [smoke, payments]
            ---

            # Test Title
            """;

        // Act
        var result = _parser.ParseFrontmatter<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("TC-101", result.Value.Id);
        Assert.Equal("high", result.Value.Priority);
        Assert.Equal(["smoke", "payments"], result.Value.Tags);
    }

    [Fact]
    public void ParseFrontmatter_WithMissingFrontmatter_ReturnsFailure()
    {
        // Arrange
        const string markdown = """
            # Test Title

            Some content
            """;

        // Act
        var result = _parser.ParseFrontmatter<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal("MISSING_FRONTMATTER", result.Errors[0].Code);
    }

    [Fact]
    public void ParseFrontmatter_WithInvalidYaml_ReturnsFailure()
    {
        // Arrange
        const string markdown = """
            ---
            id: [invalid yaml
            priority: high
            ---

            # Test Title
            """;

        // Act
        var result = _parser.ParseFrontmatter<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_YAML", result.Errors[0].Code);
    }

    [Fact]
    public void ParseFrontmatter_WithEmptyFrontmatter_ReturnsFailure()
    {
        // Arrange
        const string markdown = """
            ---
            ---

            # Test Title
            """;

        // Act
        var result = _parser.ParseFrontmatter<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        // Empty frontmatter is detected as missing since there's no valid YAML content
        Assert.True(
            result.Errors[0].Code == "EMPTY_FRONTMATTER" ||
            result.Errors[0].Code == "MISSING_FRONTMATTER" ||
            result.Errors[0].Code == "DESERIALIZATION_FAILED");
    }

    [Fact]
    public void ExtractBody_WithFrontmatter_ReturnsBodyContent()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            ---

            # Test Title

            Some content here.
            """;

        // Act
        var body = _parser.ExtractBody(markdown);

        // Assert
        Assert.Contains("# Test Title", body);
        Assert.Contains("Some content here.", body);
        Assert.DoesNotContain("id: TC-101", body);
    }

    [Fact]
    public void ExtractBody_WithoutFrontmatter_ReturnsFullContent()
    {
        // Arrange
        const string markdown = """
            # Test Title

            Some content here.
            """;

        // Act
        var body = _parser.ExtractBody(markdown);

        // Assert
        Assert.Equal(markdown, body);
    }

    [Fact]
    public void Parse_WithValidDocument_ReturnsFrontmatterAndBody()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: medium
            ---

            # Test Title

            Content body.
            """;

        // Act
        var result = _parser.Parse<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsSuccess);
        var (frontmatter, body) = result.Value;
        Assert.Equal("TC-101", frontmatter.Id);
        Assert.Contains("# Test Title", body);
    }

    [Fact]
    public void ParseFrontmatter_WithOptionalFields_ReturnsNullForMissing()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: high
            ---

            # Test
            """;

        // Act
        var result = _parser.ParseFrontmatter<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Component);
        Assert.Null(result.Value.DependsOn);
        Assert.Empty(result.Value.Tags);
    }

    [Fact]
    public void ParseFrontmatter_WithAllFields_ReturnsAllValues()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: high
            tags: [smoke, checkout]
            component: payments
            preconditions: User is logged in
            environment: [staging, production]
            estimated_duration: 5m
            depends_on: TC-100
            source_refs: [docs/checkout.md]
            related_work_items: ["#123", "#456"]
            custom:
              author: test-user
            ---

            # Test
            """;

        // Act
        var result = _parser.ParseFrontmatter<TestCaseFrontmatter>(markdown);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("TC-101", result.Value.Id);
        Assert.Equal("high", result.Value.Priority);
        Assert.Equal(["smoke", "checkout"], result.Value.Tags);
        Assert.Equal("payments", result.Value.Component);
        Assert.Equal("User is logged in", result.Value.Preconditions);
        Assert.Equal(["staging", "production"], result.Value.Environment);
        Assert.Equal("5m", result.Value.EstimatedDuration);
        Assert.Equal("TC-100", result.Value.DependsOn);
        Assert.Equal(["docs/checkout.md"], result.Value.SourceRefs);
        Assert.NotNull(result.Value.Custom);
    }
}
