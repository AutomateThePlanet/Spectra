using System.CommandLine;
using Spectra.CLI.Commands;
using Spectra.Core.Models.Profile;

namespace Spectra.CLI.Tests.Profile;

[Collection("WorkingDirectory")]
public sealed class ProfileShowTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public ProfileShowTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-profile-show-tests-{Guid.NewGuid():N}");
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

    private async Task<(int ExitCode, string Output)> RunCommandAsync(string args)
    {
        var command = new ProfileCommand();
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(command);

        var originalOut = Console.Out;
        using var sw = new StringWriter();

        try
        {
            Console.SetOut(sw);
            var exitCode = await rootCommand.InvokeAsync(args);
            return (exitCode, sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Show_NoProfile_DisplaysNoProfileMessage()
    {
        // Act
        var (exitCode, output) = await RunCommandAsync("profile show");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("No profile", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Show_WithRepositoryProfile_DisplaysProfile()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            description: "Test repository profile"
            options:
              detail_level: very_detailed
              default_priority: high
            ---
            """);

        // Act
        var (exitCode, output) = await RunCommandAsync("profile show");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Repository", output);
        Assert.Contains("Very Detailed", output);
    }

    [Fact]
    public async Task Show_WithJsonFlag_OutputsJson()
    {
        // Arrange
        var profilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(profilePath, """
            ---
            profile_version: 1
            options:
              detail_level: detailed
            ---
            """);

        // Act
        var (exitCode, output) = await RunCommandAsync("profile show --json");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("{", output);
        Assert.Contains("profile_version", output);
    }

    [Fact]
    public async Task Show_WithContextFlag_OutputsAIContext()
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
        var (exitCode, output) = await RunCommandAsync("profile show --context");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Test Generation Profile", output);
        Assert.Contains("Very Detailed", output);
    }

    [Fact]
    public async Task Show_WithSuiteFlag_ShowsSuiteProfile()
    {
        // Arrange
        var suiteDir = Path.Combine(_testDir, "test-cases", "checkout");
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
        var (exitCode, output) = await RunCommandAsync("profile show --suite test-cases/checkout");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Suite", output);
        Assert.Contains("Very Detailed", output);
    }

    [Fact]
    public async Task Show_SuiteInheritsFromRepo_ShowsBothSources()
    {
        // Arrange
        var repoProfilePath = Path.Combine(_testDir, ProfileDefaults.RepositoryProfileFileName);
        await File.WriteAllTextAsync(repoProfilePath, """
            ---
            profile_version: 1
            options:
              detail_level: detailed
              min_negative_scenarios: 3
            ---
            """);

        var suiteDir = Path.Combine(_testDir, "test-cases", "checkout");
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
        var (exitCode, output) = await RunCommandAsync("profile show --suite test-cases/checkout");

        // Assert
        Assert.Equal(0, exitCode);
        // Should show suite profile is active with very_detailed
        Assert.Contains("Very Detailed", output);
    }
}
