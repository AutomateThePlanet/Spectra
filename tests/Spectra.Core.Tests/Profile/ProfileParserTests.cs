using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.Core.Tests.Profile;

public sealed class ProfileParserTests
{
    private readonly ProfileParser _parser = new();

    [Fact]
    public void Parse_ValidProfile_ReturnsSuccess()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options:
              detail_level: detailed
            ---
            # Test Profile
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Profile);
        Assert.Equal(1, result.Profile.ProfileVersion);
        Assert.Equal(DetailLevel.Detailed, result.Profile.Options.DetailLevel);
    }

    [Fact]
    public void Parse_MissingFrontmatter_ReturnsError()
    {
        // Arrange
        var content = "# Just a heading\nSome content";

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("MISSING_FRONTMATTER", result.Error.Code);
    }

    [Fact]
    public void Parse_InvalidYaml_ReturnsError()
    {
        // Arrange
        var content = """
            ---
            profile_version: [invalid
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_YAML", result.Error.Code);
    }

    [Fact]
    public void Parse_AllDetailLevels_ParsesCorrectly()
    {
        // Arrange & Act & Assert
        AssertDetailLevel("high_level", DetailLevel.HighLevel);
        AssertDetailLevel("detailed", DetailLevel.Detailed);
        AssertDetailLevel("very_detailed", DetailLevel.VeryDetailed);
    }

    [Fact]
    public void Parse_AllPriorities_ParsesCorrectly()
    {
        // Arrange & Act & Assert
        AssertPriority("high", Priority.High);
        AssertPriority("medium", Priority.Medium);
        AssertPriority("low", Priority.Low);
    }

    [Fact]
    public void Parse_AllStepFormats_ParsesCorrectly()
    {
        // Arrange & Act & Assert
        AssertStepFormat("bullets", StepFormat.Bullets);
        AssertStepFormat("numbered", StepFormat.Numbered);
        AssertStepFormat("paragraphs", StepFormat.Paragraphs);
    }

    [Fact]
    public void Parse_DomainTypes_ParsesCorrectly()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options:
              domain:
                domains:
                  - payments
                  - authentication
                  - pii_gdpr
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Profile!.Options.Domain.Domains.Count);
        Assert.Contains(DomainType.Payments, result.Profile.Options.Domain.Domains);
        Assert.Contains(DomainType.Authentication, result.Profile.Options.Domain.Domains);
        Assert.Contains(DomainType.PiiGdpr, result.Profile.Options.Domain.Domains);
    }

    [Fact]
    public void Parse_PiiSensitivity_ParsesCorrectly()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options:
              domain:
                pii_sensitivity: strict
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PiiSensitivity.Strict, result.Profile!.Options.Domain.PiiSensitivity);
    }

    [Fact]
    public void Parse_Exclusions_ParsesCorrectly()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options:
              exclusions:
                - performance
                - load_testing
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Profile!.Options.Exclusions.Count);
        Assert.Contains("performance", result.Profile.Options.Exclusions);
        Assert.Contains("load_testing", result.Profile.Options.Exclusions);
    }

    [Fact]
    public void Parse_FormattingOptions_ParsesCorrectly()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options:
              formatting:
                step_format: bullets
                use_action_verbs: false
                include_screenshots: true
                max_steps_per_test: 15
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        var formatting = result.Profile!.Options.Formatting;
        Assert.Equal(StepFormat.Bullets, formatting.StepFormat);
        Assert.False(formatting.UseActionVerbs);
        Assert.True(formatting.IncludeScreenshots);
        Assert.Equal(15, formatting.MaxStepsPerTest);
    }

    [Fact]
    public void Parse_MinimalProfile_UsesDefaults()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            options: {}
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ProfileDefaults.DetailLevel, result.Profile!.Options.DetailLevel);
        Assert.Equal(ProfileDefaults.MinNegativeScenarios, result.Profile.Options.MinNegativeScenarios);
        Assert.Equal(ProfileDefaults.DefaultPriority, result.Profile.Options.DefaultPriority);
    }

    [Fact]
    public void Parse_WithDescription_ExtractsDescription()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            description: "My test profile"
            options: {}
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("My test profile", result.Profile!.Description);
    }

    [Fact]
    public void Parse_WithTimestamps_ParsesCorrectly()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            created_at: 2026-03-15T10:00:00Z
            updated_at: 2026-03-15T11:00:00Z
            options: {}
            ---
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc), result.Profile!.CreatedAt);
        Assert.Equal(new DateTime(2026, 3, 15, 11, 0, 0, DateTimeKind.Utc), result.Profile.UpdatedAt);
    }

    [Fact]
    public void Parse_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespaceContent_ThrowsArgumentException(string content)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(content));
    }

    [Fact]
    public void ExtractBody_ValidContent_ReturnsBody()
    {
        // Arrange
        var content = """
            ---
            profile_version: 1
            ---

            # Profile Description

            This is the body content.
            """;

        // Act
        var body = _parser.ExtractBody(content);

        // Assert
        Assert.NotNull(body);
        Assert.Contains("Profile Description", body);
        Assert.Contains("body content", body);
    }

    [Fact]
    public void ExtractBody_NoFrontmatter_ReturnsNull()
    {
        // Arrange
        var content = "Just plain content";

        // Act
        var body = _parser.ExtractBody(content);

        // Assert
        Assert.Null(body);
    }

    private void AssertDetailLevel(string value, DetailLevel expected)
    {
        var content = $"""
            ---
            profile_version: 1
            options:
              detail_level: {value}
            ---
            """;
        var result = _parser.Parse(content);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Profile!.Options.DetailLevel);
    }

    private void AssertPriority(string value, Priority expected)
    {
        var content = $"""
            ---
            profile_version: 1
            options:
              default_priority: {value}
            ---
            """;
        var result = _parser.Parse(content);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Profile!.Options.DefaultPriority);
    }

    private void AssertStepFormat(string value, StepFormat expected)
    {
        var content = $"""
            ---
            profile_version: 1
            options:
              formatting:
                step_format: {value}
            ---
            """;
        var result = _parser.Parse(content);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Profile!.Options.Formatting.StepFormat);
    }
}
