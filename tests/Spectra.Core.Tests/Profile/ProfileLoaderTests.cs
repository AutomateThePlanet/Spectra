using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.Core.Tests.Profile;

public sealed class ProfileLoaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly ProfileLoader _loader = new();

    public ProfileLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-profile-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoProfile_ReturnsDefaults()
    {
        // Act
        var result = await _loader.LoadAsync(_testDir);

        // Assert
        Assert.Equal(SourceType.Default, result.Source.Type);
        Assert.Equal(ProfileDefaults.DetailLevel, result.Profile.Options.DetailLevel);
    }

    [Fact]
    public async Task LoadAsync_WithRepositoryProfile_LoadsProfile()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            options:
              detail_level: very_detailed
              min_negative_scenarios: 5
            ---
            """);

        // Act
        var result = await _loader.LoadAsync(_testDir);

        // Assert
        Assert.Equal(SourceType.Repository, result.Source.Type);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile.Options.DetailLevel);
        Assert.Equal(5, result.Profile.Options.MinNegativeScenarios);
    }

    [Fact]
    public async Task LoadAsync_WithSuiteProfile_OverridesRepository()
    {
        // Arrange
        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var repoProfilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(repoProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: detailed
              min_negative_scenarios: 2
            ---
            """);

        var suiteProfilePath = Path.Combine(suiteDir, ProfileDefaults.SuiteProfileFileName);
        await File.WriteAllTextAsync(suiteProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: very_detailed
            ---
            """);

        // Act
        var result = await _loader.LoadAsync(_testDir, suiteDir);

        // Assert
        Assert.Equal(SourceType.Suite, result.Source.Type);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile.Options.DetailLevel);
        // min_negative_scenarios should be inherited from repo profile
        Assert.Equal(2, result.Profile.Options.MinNegativeScenarios);
    }

    [Fact]
    public async Task LoadAsync_OnlySuiteProfile_UsesDefaults()
    {
        // Arrange
        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var suiteProfilePath = Path.Combine(suiteDir, ProfileDefaults.SuiteProfileFileName);
        await File.WriteAllTextAsync(suiteProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: very_detailed
            ---
            """);

        // Act
        var result = await _loader.LoadAsync(_testDir, suiteDir);

        // Assert
        Assert.Equal(SourceType.Suite, result.Source.Type);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile.Options.DetailLevel);
    }

    [Fact]
    public async Task LoadRepositoryProfileAsync_ProfileExists_LoadsSuccessfully()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            description: "Test profile"
            options:
              detail_level: high_level
            ---
            """);

        // Act
        var result = await _loader.LoadRepositoryProfileAsync(_testDir);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Test profile", result.Profile!.Description);
        Assert.Equal(DetailLevel.HighLevel, result.Profile.Options.DetailLevel);
    }

    [Fact]
    public async Task LoadRepositoryProfileAsync_NoProfile_ReturnsNotFound()
    {
        // Act
        var result = await _loader.LoadRepositoryProfileAsync(_testDir);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.WasNotFound);
    }

    [Fact]
    public async Task LoadFromPathAsync_InvalidProfile_ReturnsErrors()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, "invalid.md");
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: [invalid
            ---
            """);

        // Act
        var result = await _loader.LoadFromPathAsync(profilePath);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void RepositoryProfileExists_NoProfile_ReturnsFalse()
    {
        // Act
        var exists = _loader.RepositoryProfileExists(_testDir);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task RepositoryProfileExists_WithProfile_ReturnsTrue()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            options: {}
            ---
            """);

        // Act
        var exists = _loader.RepositoryProfileExists(_testDir);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task LoadAsync_InheritanceChain_TracksAllSources()
    {
        // Arrange
        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var repoProfilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(repoProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: detailed
            ---
            """);

        var suiteProfilePath = Path.Combine(suiteDir, ProfileDefaults.SuiteProfileFileName);
        await File.WriteAllTextAsync(suiteProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: very_detailed
            ---
            """);

        // Act
        var result = await _loader.LoadAsync(_testDir, suiteDir);

        // Assert
        Assert.Equal(2, result.InheritanceChain.Count);
        Assert.Equal(SourceType.Suite, result.InheritanceChain[0].Type);
        Assert.Equal(SourceType.Repository, result.InheritanceChain[1].Type);
    }

    [Fact]
    public async Task LoadAsync_NullBasePath_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _loader.LoadAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_EmptyOrWhitespaceBasePath_ThrowsArgumentException(string basePath)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _loader.LoadAsync(basePath));
    }

    [Fact]
    public async Task LoadAsync_CustomRepositoryFileName_LoadsFromCustomPath()
    {
        // Arrange
        var config = new Core.Models.Config.ProfileConfig { RepositoryFile = "custom.profile.md" };
        var loader = new ProfileLoader(config);

        var customProfilePath = Path.Combine(_testDir, "custom.profile.md");
        await File.WriteAllTextAsync(customProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: very_detailed
            ---
            """);

        // Act
        var result = await loader.LoadAsync(_testDir);

        // Assert
        Assert.Equal(SourceType.Repository, result.Source.Type);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile.Options.DetailLevel);
    }

    [Fact]
    public async Task LoadAsync_CustomSuiteFileName_LoadsFromCustomPath()
    {
        // Arrange
        var config = new Core.Models.Config.ProfileConfig { SuiteFile = "custom.suite.md" };
        var loader = new ProfileLoader(config);

        var suiteDir = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(suiteDir);

        var customSuiteProfilePath = Path.Combine(suiteDir, "custom.suite.md");
        await File.WriteAllTextAsync(customSuiteProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: very_detailed
            ---
            """);

        // Act
        var result = await loader.LoadAsync(_testDir, suiteDir);

        // Assert
        Assert.Equal(SourceType.Suite, result.Source.Type);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile.Options.DetailLevel);
    }

    [Fact]
    public void RepositoryFileName_ReturnsConfiguredValue()
    {
        // Arrange
        var config = new Core.Models.Config.ProfileConfig { RepositoryFile = "my-profile.md" };
        var loader = new ProfileLoader(config);

        // Assert
        Assert.Equal("my-profile.md", loader.RepositoryFileName);
    }

    [Fact]
    public void SuiteFileName_ReturnsConfiguredValue()
    {
        // Arrange
        var config = new Core.Models.Config.ProfileConfig { SuiteFile = "my-suite.md" };
        var loader = new ProfileLoader(config);

        // Assert
        Assert.Equal("my-suite.md", loader.SuiteFileName);
    }

    [Fact]
    public async Task LoadAsync_ValidateOnLoadFalse_SkipsValidation()
    {
        // Arrange
        var config = new Core.Models.Config.ProfileConfig { ValidateOnLoad = false };
        var loader = new ProfileLoader(config);

        // Profile with invalid value (min_negative_scenarios: -1 would normally fail validation)
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            options:
              min_negative_scenarios: -1
            ---
            """);

        // Act - should not throw or return invalid because validation is skipped
        var result = await loader.LoadFromPathAsync(profilePath);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(-1, result.Profile!.Options.MinNegativeScenarios);
    }
}
