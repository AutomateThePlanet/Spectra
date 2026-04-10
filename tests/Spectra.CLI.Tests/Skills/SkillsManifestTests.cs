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
        Assert.Equal(12, SkillContent.All.Count);
        Assert.True(SkillContent.All.ContainsKey("spectra-generate"));
        Assert.True(SkillContent.All.ContainsKey("spectra-update"));
        Assert.True(SkillContent.All.ContainsKey("spectra-coverage"));
        Assert.True(SkillContent.All.ContainsKey("spectra-dashboard"));
        Assert.True(SkillContent.All.ContainsKey("spectra-validate"));
        Assert.True(SkillContent.All.ContainsKey("spectra-list"));
        Assert.True(SkillContent.All.ContainsKey("spectra-init-profile"));
        Assert.True(SkillContent.All.ContainsKey("spectra-help"));
        Assert.True(SkillContent.All.ContainsKey("spectra-criteria"));
        Assert.True(SkillContent.All.ContainsKey("spectra-docs"));
        Assert.True(SkillContent.All.ContainsKey("spectra-prompts"));
        Assert.True(SkillContent.All.ContainsKey("spectra-quickstart"));
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
    public void ExecutionAgent_LineCount_Within200()
    {
        var content = AgentContent.ExecutionAgent;
        var lineCount = content.Split('\n').Length;
        Assert.True(lineCount <= 200, $"Execution agent is {lineCount} lines, expected ≤200");
    }

    [Fact]
    public void GenerationAgent_LineCount_Within100()
    {
        var content = AgentContent.GenerationAgent;
        var lineCount = content.Split('\n').Length;
        Assert.True(lineCount <= 100, $"Generation agent is {lineCount} lines, expected ≤100");
    }

    [Fact]
    public void Agents_DoNotContain_DuplicatedCliCodeBlocks()
    {
        // Agents may reference CLI commands in delegation tables (inline `code`),
        // but must NOT contain fenced code blocks with full CLI instructions —
        // those belong in SKILLs only.
        var cliBlockMarkers = new[]
        {
            "spectra ai analyze --coverage",
            "spectra dashboard --output",
            "spectra validate --no-interaction",
            "spectra docs index",
            "spectra ai analyze --extract-criteria",
            "spectra ai analyze --list-criteria"
        };

        foreach (var agent in AgentContent.All.Values)
        {
            // Extract fenced code blocks (```...```)
            var codeBlocks = System.Text.RegularExpressions.Regex.Matches(agent, @"```[\s\S]*?```");
            foreach (System.Text.RegularExpressions.Match block in codeBlocks)
            {
                foreach (var marker in cliBlockMarkers)
                {
                    Assert.DoesNotContain(marker, block.Value);
                }
            }
        }
    }

    [Fact]
    public void AllSkills_UseStepNFormat_NotToolCallN()
    {
        // Check that no SKILL uses "### Tool call N" heading format
        foreach (var (name, content) in SkillContent.All)
        {
            if (name == "spectra-help") continue; // help has no steps
            Assert.DoesNotMatch(@"###\s+Tool call \d", content);
        }
    }

    [Fact]
    public void AllSkills_DoNotUse_TerminalLastCommand()
    {
        // Check SKILL step instructions, not the YAML frontmatter tools list
        foreach (var (name, content) in SkillContent.All)
        {
            // Split on "---" to get the body after frontmatter
            var parts = content.Split("---", 3);
            var body = parts.Length >= 3 ? parts[2] : content;
            Assert.DoesNotContain("terminalLastCommand", body);
        }
    }

    [Fact]
    public void HelpSkill_CoversAllCommandCategories()
    {
        var content = SkillContent.Help;
        var requiredCategories = new[]
        {
            "Generation", "Execution", "Coverage", "Dashboard",
            "Validation", "List", "Acceptance Criteria", "Documentation Index"
        };

        foreach (var category in requiredCategories)
        {
            Assert.Contains(category, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AllSkills_WithCommands_IncludeNoInteractionFlag()
    {
        var skillsWithCommands = new[] { "spectra-generate", "spectra-update", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs", "spectra-list", "spectra-init-profile" };

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
        var skillsWithCommands = new[] { "spectra-generate", "spectra-update", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs", "spectra-list", "spectra-init-profile" };

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
        var skillsWithCommands = new[] { "spectra-update", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs", "spectra-list", "spectra-init-profile" };

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
        var longRunningSkills = new[] { "spectra-update", "spectra-coverage", "spectra-dashboard", "spectra-docs" };

        foreach (var skillName in longRunningSkills)
        {
            var content = SkillContent.All[skillName];
            Assert.Contains(".spectra-progress.html", content);
        }
    }

    [Fact]
    public void AllSkills_WithCommands_IncludeResultFileRead()
    {
        var skillsWithResultFile = new[] { "spectra-generate", "spectra-update", "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-criteria", "spectra-docs" };

        foreach (var skillName in skillsWithResultFile)
        {
            var content = SkillContent.All[skillName];
            Assert.Contains(".spectra-result.json", content);
        }
    }

    [Fact]
    public void UpdateSkill_UsesStepFormat()
    {
        var content = SkillContent.Update;
        Assert.Contains("**Step 1**", content);
        Assert.Contains("**Step 2**", content);
        Assert.DoesNotMatch(@"###\s+Tool call \d", content);
    }

    [Fact]
    public void UpdateSkill_ContainsDoNothingInstruction()
    {
        var content = SkillContent.Update;
        Assert.Contains("do NOTHING", content);
    }

    [Fact]
    public void UpdateSkill_ToolsListContainsBrowserOpenBrowserPage()
    {
        var content = SkillContent.Update;
        Assert.Contains("browser/openBrowserPage", content);
    }

    [Fact]
    public void GenerationAgent_ContainsUpdateDelegation()
    {
        var content = AgentContent.GenerationAgent;
        Assert.Contains("spectra-update", content);
    }

    [Fact]
    public void ExecutionAgent_ContainsUpdateDelegation()
    {
        var content = AgentContent.ExecutionAgent;
        Assert.Contains("spectra-update", content);
    }

    [Fact]
    public void QuickstartSkill_NotEmpty_AndContainsWorkflows()
    {
        var content = SkillContent.Quickstart;

        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("name: spectra-quickstart", content);
        Assert.Contains("# SPECTRA Quickstart Guide", content);

        // 12 workflow sections (Workflow 1..12)
        var workflowCount = System.Text.RegularExpressions.Regex.Matches(content, @"^## Workflow \d+:", System.Text.RegularExpressions.RegexOptions.Multiline).Count;
        Assert.Equal(12, workflowCount);

        // Trigger phrases must be present
        Assert.Contains("Help me get started", content);
        Assert.Contains("Quickstart", content);
        Assert.Contains("Tutorial", content);
    }

    [Fact]
    public void GenerationAgent_References_QuickstartSkill()
    {
        Assert.Contains("spectra-quickstart", AgentContent.GenerationAgent);
    }

    [Fact]
    public void ExecutionAgent_References_QuickstartSkill()
    {
        Assert.Contains("spectra-quickstart", AgentContent.ExecutionAgent);
    }

    [Fact]
    public void Agents_DoNotContain_UpdateCliCodeBlocks()
    {
        foreach (var agent in AgentContent.All.Values)
        {
            var codeBlocks = System.Text.RegularExpressions.Regex.Matches(agent, @"```[\s\S]*?```");
            foreach (System.Text.RegularExpressions.Match block in codeBlocks)
            {
                Assert.DoesNotContain("spectra ai update", block.Value);
            }
        }
    }
}
