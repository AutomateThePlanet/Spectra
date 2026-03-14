using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.Core.Tests.Validation;

public class DependsOnValidatorTests
{
    private readonly DependsOnValidator _validator = new();

    [Fact]
    public void Validate_NoDependencies_ReturnsNoErrors()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002")
        };

        var result = _validator.Validate(tests);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidDependency_ReturnsNoErrors()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002", "TC-001")
        };

        var result = _validator.Validate(tests);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidDependency_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002", "TC-999")
        };

        var result = _validator.Validate(tests);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_DEPENDS_ON");
    }

    [Fact]
    public void Validate_SelfReference_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "TC-001")
        };

        var result = _validator.Validate(tests);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "SELF_REFERENCE");
    }

    [Fact]
    public void Validate_CircularDependency_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "TC-002"),
            CreateTestCase("TC-002", "TC-001")
        };

        var result = _validator.Validate(tests);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CIRCULAR_DEPENDENCY");
    }

    [Fact]
    public void Validate_LongCycle_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "TC-003"),
            CreateTestCase("TC-002", "TC-001"),
            CreateTestCase("TC-003", "TC-002")
        };

        var result = _validator.Validate(tests);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CIRCULAR_DEPENDENCY");
    }

    [Fact]
    public void Validate_ChainWithoutCycle_ReturnsNoErrors()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002", "TC-001"),
            CreateTestCase("TC-003", "TC-002"),
            CreateTestCase("TC-004", "TC-003")
        };

        var result = _validator.Validate(tests);

        Assert.True(result.IsValid);
    }

    private static TestCase CreateTestCase(string id, string? dependsOn = null) => new()
    {
        Id = id,
        Title = "Test",
        Priority = Priority.Medium,
        Steps = [],
        ExpectedResult = "Result",
        FilePath = $"{id}.md",
        DependsOn = dependsOn
    };
}
