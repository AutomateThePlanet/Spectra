using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Commands;

public class InitCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly ILogger<InitHandler> _logger;

    public InitCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "spectra-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        var loggerFactory = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet);
        _logger = loggerFactory.CreateLogger<InitHandler>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task HandleAsync_InEmptyDirectory_CreatesConfigAndFolders()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        // Verify config file
        var configPath = Path.Combine(_testDir, "spectra.config.json");
        Assert.True(File.Exists(configPath), "Config file should exist");

        // Verify directories
        Assert.True(Directory.Exists(Path.Combine(_testDir, "docs")), "docs/ should exist");
        Assert.True(Directory.Exists(Path.Combine(_testDir, "tests")), "tests/ should exist");

        // Verify skill file
        var skillPath = Path.Combine(_testDir, ".github", "skills", "test-generation", "SKILL.md");
        Assert.True(File.Exists(skillPath), "SKILL.md should exist");
    }

    [Fact]
    public async Task HandleAsync_WithExistingConfig_FailsWithoutForce()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Create existing config
        var configPath = Path.Combine(_testDir, "spectra.config.json");
        await File.WriteAllTextAsync(configPath, "{}");

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Error, exitCode);
    }

    [Fact]
    public async Task HandleAsync_WithExistingConfig_SucceedsWithForce()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Create existing config
        var configPath = Path.Combine(_testDir, "spectra.config.json");
        await File.WriteAllTextAsync(configPath, "{}");

        // Act
        var exitCode = await handler.HandleAsync(force: true);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        // Verify config was overwritten with default content
        var content = await File.ReadAllTextAsync(configPath);
        Assert.Contains("source", content);
        Assert.Contains("tests", content);
        Assert.Contains("ai", content);
    }

    [Fact]
    public async Task HandleAsync_WithExistingGitIgnore_AddsLockPattern()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Create existing .gitignore
        var gitIgnorePath = Path.Combine(_testDir, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, "node_modules/\n*.log\n");

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        var content = await File.ReadAllTextAsync(gitIgnorePath);
        Assert.Contains(".spectra.lock", content);
    }

    [Fact]
    public async Task HandleAsync_WithGitIgnoreContainingLockPattern_DoesNotDuplicate()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Create existing .gitignore with lock pattern
        var gitIgnorePath = Path.Combine(_testDir, ".gitignore");
        await File.WriteAllTextAsync(gitIgnorePath, ".spectra.lock\n");

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        var content = await File.ReadAllTextAsync(gitIgnorePath);
        var lockCount = content.Split(".spectra.lock").Length - 1;
        Assert.Equal(1, lockCount);
    }

    [Fact]
    public async Task HandleAsync_ConfigContainsValidJson()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        var configPath = Path.Combine(_testDir, "spectra.config.json");
        var content = await File.ReadAllTextAsync(configPath);

        // Should be valid JSON (no exception thrown)
        var config = System.Text.Json.JsonDocument.Parse(content);

        // Verify structure
        Assert.True(config.RootElement.TryGetProperty("source", out _));
        Assert.True(config.RootElement.TryGetProperty("tests", out _));
        Assert.True(config.RootElement.TryGetProperty("ai", out _));
    }
}
