using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class TestCaseParserTests
{
    private readonly TestCaseParser _parser = new();

    [Fact]
    public void Parse_WithValidTestCase_ReturnsSuccess()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: high
            tags: [smoke, payments]
            component: checkout
            source_refs: [docs/checkout.md]
            ---

            # Checkout with valid Visa card

            ## Preconditions

            - User is logged in
            - Cart contains at least one item

            ## Steps

            1. Navigate to checkout
            2. Enter valid shipping address
            3. Select Visa as payment method
            4. Enter valid card details
            5. Click "Place Order"

            ## Expected Result

            - Order is created successfully
            - Order confirmation page is displayed
            - Confirmation email is sent to user

            ## Test Data

            - Card number: 4111 1111 1111 1111
            - Expiry: 12/2028
            """;

        // Act
        var result = _parser.Parse(markdown, "checkout/checkout-happy-path.md");

        // Assert
        Assert.True(result.IsSuccess);
        var testCase = result.Value;

        Assert.Equal("TC-101", testCase.Id);
        Assert.Equal("checkout/checkout-happy-path.md", testCase.FilePath);
        Assert.Equal(Priority.High, testCase.Priority);
        Assert.Equal(["smoke", "payments"], testCase.Tags);
        Assert.Equal("checkout", testCase.Component);
        Assert.Equal("Checkout with valid Visa card", testCase.Title);
        Assert.Equal(5, testCase.Steps.Count);
        Assert.Equal("Navigate to checkout", testCase.Steps[0]);
        Assert.Contains("Order is created successfully", testCase.ExpectedResult);
        Assert.NotNull(testCase.TestData);
        Assert.Contains("4111 1111 1111 1111", testCase.TestData);
    }

    [Fact]
    public void Parse_WithMissingTitle_ReturnsFailure()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: high
            ---

            Some content without an H1 title.

            ## Steps

            1. Do something

            ## Expected Result

            Something happens
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal("MISSING_TITLE", result.Errors[0].Code);
    }

    [Fact]
    public void Parse_WithMissingExpectedResult_ReturnsFailure()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: high
            ---

            # Test Title

            ## Steps

            1. Do something
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal("MISSING_EXPECTED_RESULT", result.Errors[0].Code);
    }

    [Fact]
    public void Parse_WithInvalidPriority_ReturnsFailure()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: critical
            ---

            # Test Title

            ## Steps

            1. Do something

            ## Expected Result

            Something happens
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_PRIORITY", result.Errors[0].Code);
    }

    [Fact]
    public void Parse_WithAllPriorityLevels_ParsesCorrectly()
    {
        string CreateTestCase(string priority) => $"""
            ---
            id: TC-101
            priority: {priority}
            ---

            # Test

            ## Expected Result

            Something
            """;

        // High
        var highResult = _parser.Parse(CreateTestCase("high"), "test.md");
        Assert.True(highResult.IsSuccess);
        Assert.Equal(Priority.High, highResult.Value.Priority);

        // Medium
        var mediumResult = _parser.Parse(CreateTestCase("medium"), "test.md");
        Assert.True(mediumResult.IsSuccess);
        Assert.Equal(Priority.Medium, mediumResult.Value.Priority);

        // Low
        var lowResult = _parser.Parse(CreateTestCase("low"), "test.md");
        Assert.True(lowResult.IsSuccess);
        Assert.Equal(Priority.Low, lowResult.Value.Priority);
    }

    [Fact]
    public void Parse_WithMinimalContent_ReturnsSuccess()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: medium
            ---

            # Minimal Test

            ## Expected Result

            Something happens
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("TC-101", result.Value.Id);
        Assert.Equal("Minimal Test", result.Value.Title);
        Assert.Empty(result.Value.Steps);
        Assert.Null(result.Value.TestData);
    }

    [Fact]
    public void Parse_WithDuration_ParsesDuration()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: medium
            estimated_duration: 5m
            ---

            # Test

            ## Expected Result

            Something
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EstimatedDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Value.EstimatedDuration);
    }

    [Fact]
    public void Parse_WithComplexDuration_ParsesCorrectly()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-101
            priority: medium
            estimated_duration: 1h30m
            ---

            # Test

            ## Expected Result

            Something
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EstimatedDuration);
        Assert.Equal(TimeSpan.FromMinutes(90), result.Value.EstimatedDuration);
    }

    [Fact]
    public void Parse_WithDependency_ParsesDependsOn()
    {
        // Arrange
        const string markdown = """
            ---
            id: TC-102
            priority: medium
            depends_on: TC-101
            ---

            # Test

            ## Expected Result

            Something
            """;

        // Act
        var result = _parser.Parse(markdown, "test.md");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("TC-101", result.Value.DependsOn);
    }
}
