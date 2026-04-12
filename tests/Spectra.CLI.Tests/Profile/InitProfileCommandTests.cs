using System.CommandLine;
using Spectra.CLI.Commands;
using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.CLI.Tests.Profile;

[Collection("WorkingDirectory")]
public sealed class InitProfileCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public InitProfileCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-init-profile-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public async Task Execute_NonInteractive_CreatesDefaultProfile()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive");

        // Assert
        Assert.Equal(0, exitCode);
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        Assert.True(File.Exists(profilePath));
    }

    [Fact]
    public async Task Execute_WithDetailLevel_SetsDetailLevel()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --detail-level very_detailed");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile!.Options.DetailLevel);
    }

    [Fact]
    public async Task Execute_WithMinNegative_SetsMinNegative()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --min-negative 7");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Profile!.Options.MinNegativeScenarios);
    }

    [Fact]
    public async Task Execute_WithPriority_SetsPriority()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --priority high");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(Priority.High, result.Profile!.Options.DefaultPriority);
    }

    [Fact]
    public async Task Execute_WithStepFormat_SetsStepFormat()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --step-format bullets");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(StepFormat.Bullets, result.Profile!.Options.Formatting.StepFormat);
    }

    [Fact]
    public async Task Execute_WithForce_OverwritesExistingProfile()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            options:
              detail_level: high_level
            ---
            """);

        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --force --detail-level very_detailed");

        // Assert
        Assert.Equal(0, exitCode);

        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile!.Options.DetailLevel);
    }

    [Fact]
    public async Task Execute_ExistingWithoutForce_ReturnsError()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            options: {}
            ---
            """);

        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive");

        // Assert
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task Execute_WithSuite_CreatesSuiteProfile()
    {
        // Arrange
        var suiteDir = Path.Combine(_testDir, "test-cases", "checkout");
        Directory.CreateDirectory(suiteDir);

        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync($"init-profile --non-interactive --suite test-cases/checkout --detail-level very_detailed");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(suiteDir, ProfileDefaults.SuiteProfileFileName);
        Assert.True(File.Exists(profilePath));

        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(DetailLevel.VeryDetailed, result.Profile!.Options.DetailLevel);
    }

    [Fact]
    public async Task Execute_WithDescription_IncludesDescription()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --description \"My test profile\"");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal("My test profile", result.Profile!.Description);
    }

    [Fact]
    public async Task Execute_WithExclusions_SetsExclusions()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --exclusions performance --exclusions load_testing");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Contains("performance", result.Profile!.Options.Exclusions);
        Assert.Contains("load_testing", result.Profile.Options.Exclusions);
    }

    [Fact]
    public async Task Execute_WithDomains_SetsDomains()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --domains payments --domains authentication");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Contains(DomainType.Payments, result.Profile!.Options.Domain.Domains);
        Assert.Contains(DomainType.Authentication, result.Profile.Options.Domain.Domains);
    }

    [Fact]
    public async Task Execute_WithPiiSensitivity_SetsPiiSensitivity()
    {
        // Arrange
        var command = new InitProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        // Act
        var exitCode = await rootCommand.InvokeAsync("init-profile --non-interactive --pii-sensitivity strict");

        // Assert
        Assert.Equal(0, exitCode);

        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        var content = await File.ReadAllTextAsync(profilePath);
        var parser = new ProfileParser();
        var result = parser.Parse(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(PiiSensitivity.Strict, result.Profile!.Options.Domain.PiiSensitivity);
    }
}
