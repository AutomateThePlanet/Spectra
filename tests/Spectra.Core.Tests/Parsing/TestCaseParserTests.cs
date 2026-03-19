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
    public void Parse_WithMissingExpectedResult_ReturnsSuccessWithNull()
    {
        // Arrange - expected result is optional to allow partial parsing
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

        // Assert - parser should be lenient and return what it can
        Assert.True(result.IsSuccess);
        Assert.Equal("TC-101", result.Value.Id);
        Assert.Equal("Test Title", result.Value.Title);
        Assert.Single(result.Value.Steps);
        Assert.Equal("", result.Value.ExpectedResult); // Empty string when missing
    }

    [Fact]
    public void Parse_WithInvalidPriority_DefaultsToMedium()
    {
        // Arrange - invalid priority should default to medium for lenient parsing
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

        // Assert - parser should be lenient and default invalid priority to medium
        Assert.True(result.IsSuccess);
        Assert.Equal("TC-101", result.Value.Id);
        Assert.Equal(Priority.Medium, result.Value.Priority);
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

    [Fact]
    public void Parse_CompleteTestCase_AllSectionsPopulated()
    {
        // Test 1: Parse complete test case — all sections populated
        const string markdown = """
            ---
            id: TC-001
            priority: high
            tags: [smoke, api]
            component: auth
            source_refs: [docs/auth.md]
            ---

            # User login with valid credentials

            ## Preconditions

            - User account exists in the system
            - User has verified email address

            ## Steps

            1. Navigate to login page
            2. Enter valid username
            3. Enter valid password
            4. Click submit button

            ## Expected Result

            - User is redirected to dashboard
            - Welcome message displays username
            - Session cookie is set

            ## Test Data

            - Username: testuser@example.com
            - Password: ValidPass123!
            """;

        var result = _parser.Parse(markdown, "auth/login.md");

        Assert.True(result.IsSuccess);
        var tc = result.Value;

        // Title
        Assert.Equal("User login with valid credentials", tc.Title);

        // Preconditions from body
        Assert.NotNull(tc.Preconditions);
        Assert.Contains("User account exists", tc.Preconditions);
        Assert.Contains("verified email address", tc.Preconditions);

        // Steps
        Assert.Equal(4, tc.Steps.Count);
        Assert.Equal("Navigate to login page", tc.Steps[0]);
        Assert.Equal("Enter valid username", tc.Steps[1]);
        Assert.Equal("Enter valid password", tc.Steps[2]);
        Assert.Equal("Click submit button", tc.Steps[3]);

        // Expected Result
        Assert.Contains("redirected to dashboard", tc.ExpectedResult);
        Assert.Contains("Welcome message", tc.ExpectedResult);
        Assert.Contains("Session cookie", tc.ExpectedResult);

        // Test Data
        Assert.NotNull(tc.TestData);
        Assert.Contains("testuser@example.com", tc.TestData);
        Assert.Contains("ValidPass123!", tc.TestData);
    }

    [Fact]
    public void Parse_MissingOptionalSections_OnlyStepsAndExpectedResultPresent()
    {
        // Test 2: Parse test case with missing optional sections
        const string markdown = """
            ---
            id: TC-002
            priority: medium
            ---

            # Basic functionality test

            ## Steps

            1. Open the application
            2. Perform the action

            ## Expected Result

            The action completes successfully
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        var tc = result.Value;

        Assert.Equal("Basic functionality test", tc.Title);
        Assert.Equal(2, tc.Steps.Count);
        Assert.Equal("Open the application", tc.Steps[0]);
        Assert.Equal("Perform the action", tc.Steps[1]);
        Assert.Equal("The action completes successfully", tc.ExpectedResult);

        // Optional sections should be null/empty
        Assert.Null(tc.Preconditions);
        Assert.Null(tc.TestData);
    }

    [Fact]
    public void Parse_StepsAsNumberedList_ParsesIntoStepsList()
    {
        // Test 3: Parse steps as numbered list
        const string markdown = """
            ---
            id: TC-003
            priority: low
            ---

            # Multi-step process

            ## Steps

            1. First action with details
            2. Second action with more details
            3. Third action involving multiple components
            4. Fourth action to verify state
            5. Fifth action to complete flow

            ## Expected Result

            All steps complete
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        var tc = result.Value;

        Assert.Equal(5, tc.Steps.Count);
        Assert.Equal("First action with details", tc.Steps[0]);
        Assert.Equal("Second action with more details", tc.Steps[1]);
        Assert.Equal("Third action involving multiple components", tc.Steps[2]);
        Assert.Equal("Fourth action to verify state", tc.Steps[3]);
        Assert.Equal("Fifth action to complete flow", tc.Steps[4]);
    }

    [Fact]
    public void Parse_MultiLineExpectedResult_BulletPointsPreserved()
    {
        // Test 4: Parse multi-line expected result
        const string markdown = """
            ---
            id: TC-004
            priority: high
            ---

            # Complex validation test

            ## Steps

            1. Submit the form

            ## Expected Result

            - First validation passes
            - Second validation passes
            - Error message does not appear
            - Success notification shows
            - Data is persisted to database
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        var tc = result.Value;

        // Expected result should preserve the full text including bullets
        Assert.Contains("First validation passes", tc.ExpectedResult);
        Assert.Contains("Second validation passes", tc.ExpectedResult);
        Assert.Contains("Error message does not appear", tc.ExpectedResult);
        Assert.Contains("Success notification shows", tc.ExpectedResult);
        Assert.Contains("Data is persisted", tc.ExpectedResult);
    }

    [Fact]
    public void Parse_TestDataSection_RawTextPreserved()
    {
        // Test 5: Parse test data section
        const string markdown = """
            ---
            id: TC-005
            priority: medium
            ---

            # Data-driven test

            ## Steps

            1. Input the test data

            ## Expected Result

            Data is processed

            ## Test Data

            | Field | Value |
            |-------|-------|
            | Name  | John  |
            | Age   | 30    |

            Additional notes:
            - Use valid format
            - Check encoding
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        var tc = result.Value;

        Assert.NotNull(tc.TestData);
        // Raw text should be preserved including tables and notes
        Assert.Contains("Field", tc.TestData);
        Assert.Contains("John", tc.TestData);
        Assert.Contains("Additional notes", tc.TestData);
        Assert.Contains("valid format", tc.TestData);
    }

    [Fact]
    public void Parse_ExtraH2Sections_IgnoredWithoutError()
    {
        // Test 6: Parse test case with extra H2 sections
        const string markdown = """
            ---
            id: TC-006
            priority: medium
            ---

            # Test with extra sections

            ## Overview

            This is an extra section that should be ignored.

            ## Preconditions

            - System is running

            ## Steps

            1. Do something

            ## Expected Result

            Something happens

            ## Notes

            This is another extra section.

            ## References

            - Link to docs
            - Link to spec
            """;

        var result = _parser.Parse(markdown, "test.md");

        // Should succeed despite unknown sections
        Assert.True(result.IsSuccess);
        var tc = result.Value;

        Assert.Equal("Test with extra sections", tc.Title);
        Assert.Contains("System is running", tc.Preconditions);
        Assert.Single(tc.Steps);
        Assert.Equal("Do something", tc.Steps[0]);
        Assert.Equal("Something happens", tc.ExpectedResult);
    }

    [Fact]
    public void Parse_PreconditionsFromBody_OverridesFrontmatter()
    {
        // Verify body preconditions take precedence over frontmatter
        const string markdown = """
            ---
            id: TC-007
            priority: medium
            preconditions: "Frontmatter preconditions"
            ---

            # Test with body preconditions

            ## Preconditions

            Body preconditions should be used instead

            ## Steps

            1. Test step

            ## Expected Result

            Result
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        // Body preconditions should override frontmatter
        Assert.Contains("Body preconditions should be used", result.Value.Preconditions);
        Assert.DoesNotContain("Frontmatter preconditions", result.Value.Preconditions ?? "");
    }

    [Fact]
    public void Parse_PreconditionsFallbackToFrontmatter_WhenBodyMissing()
    {
        // Verify frontmatter preconditions used when body section missing
        const string markdown = """
            ---
            id: TC-008
            priority: medium
            preconditions: "Frontmatter preconditions only"
            ---

            # Test without body preconditions

            ## Steps

            1. Test step

            ## Expected Result

            Result
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.Equal("Frontmatter preconditions only", result.Value.Preconditions);
    }
}
