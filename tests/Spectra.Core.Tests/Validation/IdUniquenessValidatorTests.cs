using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.Core.Tests.Validation;

public class IdUniquenessValidatorTests
{
    private readonly IdUniquenessValidator _validator = new();

    [Fact]
    public void Validate_UniqueIds_ReturnsNoErrors()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002"),
            CreateTestCase("TC-003")
        };

        var result = _validator.Validate(tests);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DuplicateIds_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "file1.md"),
            CreateTestCase("TC-001", "file2.md")
        };

        var result = _validator.Validate(tests);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "DUPLICATE_ID");
    }

    [Fact]
    public void Validate_EmptyIds_AreSkipped()
    {
        var tests = new[]
        {
            CreateTestCase(""),
            CreateTestCase("")
        };

        var result = _validator.Validate(tests);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ErrorIncludesFirstFilePath()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "first.md"),
            CreateTestCase("TC-001", "second.md")
        };

        var result = _validator.Validate(tests);

        var error = result.Errors.First();
        Assert.Contains("first.md", error.Message);
    }

    [Fact]
    public void ValidateSuite_IncludesSuiteNameInErrors()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "file1.md"),
            CreateTestCase("TC-001", "file2.md")
        };

        var result = _validator.ValidateSuite("checkout", tests);

        Assert.Contains(result.Errors, e => e.Message.Contains("[checkout]"));
    }

    private static TestCase CreateTestCase(string id, string filePath = "test.md") => new()
    {
        Id = id,
        Title = "Test",
        Priority = Priority.Medium,
        Steps = [],
        ExpectedResult = "Result",
        FilePath = filePath
    };
}
