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

    [Fact]
    public async Task HandleAsync_InstallsExecutionAgentFile()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        // Verify execution agent file
        var agentPath = Path.Combine(_testDir, ".github", "agents", "spectra-execution.agent.md");
        Assert.True(File.Exists(agentPath), "Execution agent file should exist");

        var content = await File.ReadAllTextAsync(agentPath);
        Assert.Contains("spectra-execution", content);
        Assert.Contains("Test Execution Agent", content);
    }

    [Fact]
    public async Task HandleAsync_InstallsExecutionSkillFile()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        // Verify execution skill file
        var skillPath = Path.Combine(_testDir, ".github", "skills", "spectra-execution", "SKILL.md");
        Assert.True(File.Exists(skillPath), "Execution skill file should exist");

        var content = await File.ReadAllTextAsync(skillPath);
        Assert.Contains("spectra-execution", content);
    }

    [Fact]
    public async Task HandleAsync_WithExistingAgentFile_SkipsWithoutForce()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Create existing agent file
        var agentDir = Path.Combine(_testDir, ".github", "agents");
        Directory.CreateDirectory(agentDir);
        var agentPath = Path.Combine(agentDir, "spectra-execution.agent.md");
        await File.WriteAllTextAsync(agentPath, "# Custom Agent Content");

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        // Verify original content preserved
        var content = await File.ReadAllTextAsync(agentPath);
        Assert.Equal("# Custom Agent Content", content);
    }

    [Fact]
    public async Task HandleAsync_WithExistingAgentFile_OverwritesWithForce()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Create existing agent file
        var agentDir = Path.Combine(_testDir, ".github", "agents");
        Directory.CreateDirectory(agentDir);
        var agentPath = Path.Combine(agentDir, "spectra-execution.agent.md");
        await File.WriteAllTextAsync(agentPath, "# Custom Agent Content");

        // Act
        var exitCode = await handler.HandleAsync(force: true);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        // Verify content was overwritten
        var content = await File.ReadAllTextAsync(agentPath);
        Assert.Contains("SPECTRA Test Execution Agent", content);
    }

    [Fact]
    public async Task HandleAsync_AgentFilesHaveVersionComment()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        var agentPath = Path.Combine(_testDir, ".github", "agents", "spectra-execution.agent.md");
        var content = await File.ReadAllTextAsync(agentPath);
        Assert.Contains("name: spectra-execution", content);
    }

    [Fact]
    public async Task HandleAsync_CreatesDeployWorkflow()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);

        var workflowPath = Path.Combine(_testDir, ".github", "workflows", "deploy-dashboard.yml");
        Assert.True(File.Exists(workflowPath), "Deploy workflow should exist");

        var content = await File.ReadAllTextAsync(workflowPath);
        Assert.Contains("Deploy SPECTRA Dashboard", content);
        Assert.Contains("cloudflare/wrangler-action", content);
        // Verify no actual secrets embedded
        Assert.DoesNotContain("sk-", content);
        Assert.DoesNotContain("ghp_", content);
    }

    [Fact]
    public async Task HandleAsync_ExistingDeployWorkflow_DoesNotOverwrite()
    {
        // Arrange
        var handler = new InitHandler(_logger, _testDir);
        var workflowDir = Path.Combine(_testDir, ".github", "workflows");
        Directory.CreateDirectory(workflowDir);
        var workflowPath = Path.Combine(workflowDir, "deploy-dashboard.yml");
        await File.WriteAllTextAsync(workflowPath, "# Custom workflow");

        // Act
        var exitCode = await handler.HandleAsync(force: false);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        var content = await File.ReadAllTextAsync(workflowPath);
        Assert.Equal("# Custom workflow", content);
    }

    [Fact]
    public async Task HandleAsync_CreatesDefaultProfileYaml()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false);

        Assert.Equal(ExitCodes.Success, exitCode);
        var profilePath = Path.Combine(_testDir, "profiles", "_default.yaml");
        Assert.True(File.Exists(profilePath), "profiles/_default.yaml should exist");
        var content = await File.ReadAllTextAsync(profilePath);
        Assert.Contains("format:", content);
        Assert.Contains("fields:", content);
    }

    [Fact]
    public async Task HandleAsync_CreatesCustomizationGuide()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false);

        Assert.Equal(ExitCodes.Success, exitCode);
        var guidePath = Path.Combine(_testDir, "CUSTOMIZATION.md");
        Assert.True(File.Exists(guidePath), "CUSTOMIZATION.md should exist at project root");
        var content = await File.ReadAllTextAsync(guidePath);
        Assert.Contains("# SPECTRA Customization Guide", content);
        Assert.Contains("profiles/_default.yaml", content);
    }

    [Fact]
    public async Task HandleAsync_RegistersNewFilesInSkillsManifest()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false);

        Assert.Equal(ExitCodes.Success, exitCode);
        var manifestPath = Path.Combine(_testDir, ".spectra", "skills-manifest.json");
        Assert.True(File.Exists(manifestPath), "skills-manifest.json should exist");
        var manifestContent = await File.ReadAllTextAsync(manifestPath);
        Assert.Contains("_default.yaml", manifestContent);
        Assert.Contains("CUSTOMIZATION.md", manifestContent);
    }

    [Fact]
    public async Task HandleAsync_CreatesQuickstartSkill()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false);

        Assert.Equal(ExitCodes.Success, exitCode);
        var quickstartPath = Path.Combine(_testDir, ".github", "skills", "spectra-quickstart", "SKILL.md");
        Assert.True(File.Exists(quickstartPath), "spectra-quickstart SKILL.md should exist");
        var content = await File.ReadAllTextAsync(quickstartPath);
        Assert.Contains("name: spectra-quickstart", content);
        Assert.Contains("# SPECTRA Quickstart Guide", content);

        // Manifest should track it
        var manifestPath = Path.Combine(_testDir, ".spectra", "skills-manifest.json");
        var manifestContent = await File.ReadAllTextAsync(manifestPath);
        Assert.Contains("spectra-quickstart", manifestContent);
    }

    [Fact]
    public async Task HandleAsync_SkipSkills_DoesNotCreateQuickstartSkill()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false, skipSkills: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        var quickstartPath = Path.Combine(_testDir, ".github", "skills", "spectra-quickstart", "SKILL.md");
        Assert.False(File.Exists(quickstartPath), "spectra-quickstart SKILL.md should NOT exist when --skip-skills");
    }

    [Fact]
    public async Task HandleAsync_CreatesUsageGuide()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false);

        Assert.Equal(ExitCodes.Success, exitCode);
        var usagePath = Path.Combine(_testDir, "USAGE.md");
        Assert.True(File.Exists(usagePath), "USAGE.md should exist at project root");
        var content = await File.ReadAllTextAsync(usagePath);
        Assert.Contains("# SPECTRA Usage Guide", content);
        Assert.Contains("Generating Test Cases", content);

        // Manifest should track it
        var manifestPath = Path.Combine(_testDir, ".spectra", "skills-manifest.json");
        var manifestContent = await File.ReadAllTextAsync(manifestPath);
        Assert.Contains("USAGE.md", manifestContent);
    }

    [Fact]
    public async Task HandleAsync_SkipSkills_DoesNotCreateUsageGuide()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false, skipSkills: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        var usagePath = Path.Combine(_testDir, "USAGE.md");
        Assert.False(File.Exists(usagePath), "USAGE.md should NOT exist when --skip-skills");
    }
}
