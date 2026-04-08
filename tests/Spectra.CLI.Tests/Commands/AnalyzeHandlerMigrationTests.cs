using Spectra.CLI.Commands.Analyze;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Commands;

[Collection("WorkingDirectory")]
public class AnalyzeHandlerMigrationTests : IDisposable
{
    private readonly string _tempDir;

    public AnalyzeHandlerMigrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task Migration_RequirementsFolderExists_RenamedToCriteria()
    {
        // Arrange
        var oldDir = Path.Combine(_tempDir, "docs", "requirements");
        Directory.CreateDirectory(oldDir);
        await File.WriteAllTextAsync(Path.Combine(oldDir, "_criteria_index.yaml"), "version: 1");
        await File.WriteAllTextAsync(Path.Combine(oldDir, "checkout.criteria.yaml"), "criteria: []");

        // Act
        await AnalyzeHandler.MigrateCriteriaFolderAsync(_tempDir, VerbosityLevel.Quiet);

        // Assert
        Assert.False(Directory.Exists(oldDir));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "docs", "criteria")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "docs", "criteria", "_criteria_index.yaml")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "docs", "criteria", "checkout.criteria.yaml")));
    }

    [Fact]
    public async Task Migration_BothFoldersExist_NoMigration()
    {
        // Arrange
        var oldDir = Path.Combine(_tempDir, "docs", "requirements");
        var newDir = Path.Combine(_tempDir, "docs", "criteria");
        Directory.CreateDirectory(oldDir);
        Directory.CreateDirectory(newDir);
        await File.WriteAllTextAsync(Path.Combine(oldDir, "old.yaml"), "old");
        await File.WriteAllTextAsync(Path.Combine(newDir, "new.yaml"), "new");

        // Act
        await AnalyzeHandler.MigrateCriteriaFolderAsync(_tempDir, VerbosityLevel.Quiet);

        // Assert — both unchanged
        Assert.True(Directory.Exists(oldDir));
        Assert.True(Directory.Exists(newDir));
        Assert.True(File.Exists(Path.Combine(oldDir, "old.yaml")));
        Assert.True(File.Exists(Path.Combine(newDir, "new.yaml")));
    }

    [Fact]
    public async Task Migration_NeitherExists_NoAction()
    {
        // Arrange — docs dir exists but no requirements or criteria
        Directory.CreateDirectory(Path.Combine(_tempDir, "docs"));

        // Act
        await AnalyzeHandler.MigrateCriteriaFolderAsync(_tempDir, VerbosityLevel.Quiet);

        // Assert
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "docs", "requirements")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "docs", "criteria")));
    }

    [Fact]
    public async Task Migration_ConfigUpdated_RequirementsPathRewritten()
    {
        // Arrange
        var oldDir = Path.Combine(_tempDir, "docs", "requirements");
        Directory.CreateDirectory(oldDir);
        var configPath = Path.Combine(_tempDir, "spectra.config.json");
        await File.WriteAllTextAsync(configPath, """
            {
              "coverage": {
                "criteria_file": "docs/requirements/_criteria_index.yaml",
                "criteria_dir": "docs/requirements"
              }
            }
            """);

        // Act
        await AnalyzeHandler.MigrateCriteriaFolderAsync(_tempDir, VerbosityLevel.Quiet);

        // Assert
        var configText = await File.ReadAllTextAsync(configPath);
        Assert.DoesNotContain("docs/requirements", configText);
        Assert.Contains("docs/criteria", configText);
    }

    [Fact]
    public async Task Migration_IndexCriteriaYamlDeleted()
    {
        // Arrange — criteria dir already exists with _index.criteria.yaml
        var criteriaDir = Path.Combine(_tempDir, "docs", "criteria");
        Directory.CreateDirectory(criteriaDir);
        var indexCriteria = Path.Combine(criteriaDir, "_index.criteria.yaml");
        await File.WriteAllTextAsync(indexCriteria, "criteria: []");

        // Act
        await AnalyzeHandler.MigrateCriteriaFolderAsync(_tempDir, VerbosityLevel.Quiet);

        // Assert
        Assert.False(File.Exists(indexCriteria));
    }
}
