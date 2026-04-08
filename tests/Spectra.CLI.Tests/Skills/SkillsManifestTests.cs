using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

[Collection("WorkingDirectory")]
public class SkillsManifestTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SkillsManifestStore _store;

    public SkillsManifestTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new SkillsManifestStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var manifest = new SkillsManifest();
        manifest.Files["/path/to/SKILL.md"] = "abc123";

        await _store.SaveAsync(manifest);
        var loaded = await _store.LoadAsync();

        Assert.Single(loaded.Files);
        Assert.Equal("abc123", loaded.Files["/path/to/SKILL.md"]);
    }

    [Fact]
    public async Task Load_NoFile_ReturnsEmpty()
    {
        var loaded = await _store.LoadAsync();
        Assert.Empty(loaded.Files);
    }

    [Fact]
    public async Task Save_CreatesDirectory()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        var store = new SkillsManifestStore(subDir);

        var manifest = new SkillsManifest();
        manifest.Files["test"] = "hash";
        await store.SaveAsync(manifest);

        Assert.True(Directory.Exists(Path.Combine(subDir, ".spectra")));
    }

    [Fact]
    public void SkillContent_HasAllSkills()
    {
        Assert.Equal(9, SkillContent.All.Count);
        Assert.True(SkillContent.All.ContainsKey("spectra-generate"));
        Assert.True(SkillContent.All.ContainsKey("spectra-coverage"));
        Assert.True(SkillContent.All.ContainsKey("spectra-dashboard"));
        Assert.True(SkillContent.All.ContainsKey("spectra-validate"));
        Assert.True(SkillContent.All.ContainsKey("spectra-list"));
        Assert.True(SkillContent.All.ContainsKey("spectra-init-profile"));
        Assert.True(SkillContent.All.ContainsKey("spectra-help"));
        Assert.True(SkillContent.All.ContainsKey("spectra-criteria"));
        Assert.True(SkillContent.All.ContainsKey("spectra-docs"));
    }

    [Fact]
    public void AgentContent_HasAllAgents()
    {
        Assert.Equal(2, AgentContent.All.Count);
        Assert.True(AgentContent.All.ContainsKey("spectra-execution.agent.md"));
        Assert.True(AgentContent.All.ContainsKey("spectra-generation.agent.md"));
    }

    [Fact]
    public void AllSkills_ContainSpectraCommand()
    {
        foreach (var (name, content) in SkillContent.All)
        {
            // All SKILLs should reference spectra CLI or be a help reference
            Assert.Contains("spectra", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AllSkills_WithCommands_IncludeNoInteractionFlag()
    {
        var skillsWithCommands = new[] { "spectra-generate", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs" };

        foreach (var skillName in skillsWithCommands)
        {
            var content = SkillContent.All[skillName];
            // Every command line in these SKILLs should include --no-interaction
            var commandLines = content.Split('\n')
                .Where(l => l.TrimStart().StartsWith("spectra "))
                .ToList();

            Assert.All(commandLines, line =>
                Assert.Contains("--no-interaction", line));
        }
    }

    [Fact]
    public void AllSkills_WithCommands_IncludeOutputFormatJson()
    {
        var skillsWithCommands = new[] { "spectra-generate", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs" };

        foreach (var skillName in skillsWithCommands)
        {
            var content = SkillContent.All[skillName];
            var commandLines = content.Split('\n')
                .Where(l => l.TrimStart().StartsWith("spectra "))
                .ToList();

            Assert.All(commandLines, line =>
                Assert.Contains("--output-format json", line));
        }
    }

    [Fact]
    public void AllSkills_WithCommands_IncludeVerbosityQuiet()
    {
        var skillsWithCommands = new[] { "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs" };

        foreach (var skillName in skillsWithCommands)
        {
            var content = SkillContent.All[skillName];
            var commandLines = content.Split('\n')
                .Where(l => l.TrimStart().StartsWith("spectra "))
                .ToList();

            Assert.All(commandLines, line =>
                Assert.Contains("--verbosity quiet", line));
        }
    }

    [Fact]
    public void LongRunningSkills_IncludeProgressPageStep()
    {
        var longRunningSkills = new[] { "spectra-coverage", "spectra-dashboard", "spectra-docs" };

        foreach (var skillName in longRunningSkills)
        {
            var content = SkillContent.All[skillName];
            Assert.Contains(".spectra-progress.html", content);
        }
    }

    [Fact]
    public void AllSkills_WithCommands_IncludeResultFileRead()
    {
        var skillsWithResultFile = new[] { "spectra-generate", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs" };

        foreach (var skillName in skillsWithResultFile)
        {
            var content = SkillContent.All[skillName];
            Assert.Contains(".spectra-result.json", content);
        }
    }
}
