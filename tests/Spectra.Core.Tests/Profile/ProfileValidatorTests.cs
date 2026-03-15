using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.Core.Tests.Profile;

public sealed class ProfileValidatorTests
{
    private readonly ProfileValidator _validator = new();

    [Fact]
    public void Validate_ValidProfile_ReturnsSuccess()
    {
        // Arrange
        var profile = ProfileDefaults.CreateDefaultProfile();

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.Profile);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidVersion_ReturnsError()
    {
        // Arrange
        var profile = new GenerationProfile { ProfileVersion = 0 };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_VERSION");
    }

    [Fact]
    public void Validate_NegativeMinNegativeScenarios_ReturnsError()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions { MinNegativeScenarios = -1 }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_MIN_NEGATIVE");
    }

    [Fact]
    public void Validate_TooHighMinNegativeScenarios_ReturnsError()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions { MinNegativeScenarios = 25 }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_MIN_NEGATIVE");
    }

    [Fact]
    public void Validate_MaxStepsPerTestZero_ReturnsError()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                Formatting = new FormattingOptions { MaxStepsPerTest = 0 }
            }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_MAX_STEPS");
    }

    [Fact]
    public void Validate_MaxStepsPerTestTooHigh_ReturnsError()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                Formatting = new FormattingOptions { MaxStepsPerTest = 100 }
            }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_MAX_STEPS");
    }

    [Fact]
    public void Validate_TooManyExclusions_ReturnsError()
    {
        // Arrange
        var exclusions = Enumerable.Range(1, 25).Select(i => $"exclusion{i}").ToList();
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions { Exclusions = exclusions }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOO_MANY_EXCLUSIONS");
    }

    [Fact]
    public void Validate_UnknownExclusion_ReturnsWarning()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions { Exclusions = ["unknown_category"] }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.True(result.IsValid); // Warning, not error
        Assert.Contains(result.Warnings, w => w.Code == "UNKNOWN_EXCLUSION");
    }

    [Fact]
    public void Validate_ValidExclusions_NoWarning()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions { Exclusions = ["performance", "load_testing"] }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "UNKNOWN_EXCLUSION");
    }

    [Fact]
    public void Validate_MaxStepsPerTestNull_IsValid()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                Formatting = new FormattingOptions { MaxStepsPerTest = null }
            }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MaxStepsPerTestValid_IsValid()
    {
        // Arrange
        var profile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                Formatting = new FormattingOptions { MaxStepsPerTest = 25 }
            }
        };

        // Act
        var result = _validator.Validate(profile);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateContent_ValidContent_ReturnsSuccess()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options:
              detail_level: detailed
            ---
            """;

        // Act
        var result = _validator.ValidateContent(content);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateContent_InvalidYaml_ReturnsError()
    {
        // Arrange
        var content = """
            ---
            profile_version: [invalid
            ---
            """;

        // Act
        var result = _validator.ValidateContent(content);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NullProfile_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _validator.Validate(null!));
    }
}
