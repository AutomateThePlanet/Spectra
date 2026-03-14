using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Validation;

namespace Spectra.Core.Tests.Validation;

public class TestValidatorTests
{
    private readonly TestValidator _validator;

    public TestValidatorTests()
    {
        _validator = new TestValidator();
    }

    [Fact]
    public void Validate_ValidTestCase_ReturnsNoErrors()
    {
        var testCase = CreateValidTestCase();

        var result = _validator.Validate(testCase);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingId_ReturnsError()
    {
        var testCase = CreateTestCase(id: "", title: "Test", expectedResult: "Result");

        var result = _validator.Validate(testCase);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "MISSING_ID");
    }

    [Fact]
    public void Validate_InvalidIdFormat_ReturnsError()
    {
        var testCase = CreateTestCase(id: "INVALID-ID");

        var result = _validator.Validate(testCase);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_ID_FORMAT");
    }

    [Fact]
    public void Validate_MissingTitle_ReturnsError()
    {
        var testCase = CreateTestCase(title: "");

        var result = _validator.Validate(testCase);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "MISSING_TITLE");
    }

    [Fact]
    public void Validate_MissingExpectedResult_ReturnsError()
    {
        var testCase = CreateTestCase(expectedResult: "");

        var result = _validator.Validate(testCase);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "MISSING_EXPECTED_RESULT");
    }

    [Fact]
    public void Validate_NoSteps_ReturnsWarning()
    {
        var testCase = CreateTestCase(steps: []);

        var result = _validator.Validate(testCase);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "NO_STEPS");
    }

    [Fact]
    public void Validate_TooManySteps_ReturnsWarning()
    {
        var steps = Enumerable.Range(1, 25).Select(i => $"Step {i}").ToList();
        var testCase = CreateTestCase(steps: steps);

        var result = _validator.Validate(testCase);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "TOO_MANY_STEPS");
    }

    [Fact]
    public void Validate_CustomIdPattern_Validates()
    {
        var config = new ValidationConfig { IdPattern = @"^TEST-\d+$" };
        var validator = new TestValidator(config);
        var testCase = CreateTestCase(id: "TEST-123");

        var result = validator.Validate(testCase);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CustomIdPattern_RejectsInvalid()
    {
        var config = new ValidationConfig { IdPattern = @"^TEST-\d+$" };
        var validator = new TestValidator(config);
        var testCase = CreateTestCase(id: "TC-001");

        var result = validator.Validate(testCase);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_ID_FORMAT");
    }

    [Fact]
    public void ValidateAll_MultipleTests_CombinesResults()
    {
        var tests = new[]
        {
            CreateValidTestCase(),
            CreateTestCase(id: "TC-002", title: ""),
            CreateTestCase(id: "TC-003", expectedResult: "")
        };

        var result = _validator.ValidateAll(tests);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Validate_ErrorIncludesFilePath()
    {
        var testCase = CreateTestCase(id: "", filePath: "test.md");

        var result = _validator.Validate(testCase);

        Assert.Contains(result.Errors, e => e.FilePath == "test.md");
    }

    private static TestCase CreateValidTestCase() => CreateTestCase();

    private static TestCase CreateTestCase(
        string id = "TC-001",
        string title = "Test valid checkout flow",
        string expectedResult = "Checkout succeeds",
        string filePath = "test.md",
        Priority priority = Priority.High,
        IReadOnlyList<string>? steps = null)
    {
        return new TestCase
        {
            Id = id,
            Title = title,
            Priority = priority,
            Steps = steps ?? ["Step 1", "Step 2"],
            ExpectedResult = expectedResult,
            FilePath = filePath
        };
    }
}
