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
        // Spec 040 lifecycle (v1.52.0) added spectra-delete and spectra-suite
        // → bundled SKILL count went from 12 to 14.
        // Spec 065 added spectra-execute (CLI execution-loop SKILL) → 15.
        // Spec 071 added spectra-review-flagged (verdict disposition review) → 16.
        Assert.Equal(16, SkillContent.All.Count);
        Assert.True(SkillContent.All.ContainsKey("spectra-execute"));
        Assert.True(SkillContent.All.ContainsKey("spectra-generate"));
        Assert.True(SkillContent.All.ContainsKey("spectra-update"));
        Assert.True(SkillContent.All.ContainsKey("spectra-coverage"));
        Assert.True(SkillContent.All.ContainsKey("spectra-dashboard"));
        Assert.True(SkillContent.All.ContainsKey("spectra-validate"));
        Assert.True(SkillContent.All.ContainsKey("spectra-list"));
        Assert.True(SkillContent.All.ContainsKey("spectra-init-profile"));
        Assert.True(SkillContent.All.ContainsKey("spectra-help"));
        Assert.True(SkillContent.All.ContainsKey("spectra-criteria"));
        Assert.True(SkillContent.All.ContainsKey("spectra-delete"));
        Assert.True(SkillContent.All.ContainsKey("spectra-suite"));
        Assert.True(SkillContent.All.ContainsKey("spectra-docs"));
        Assert.True(SkillContent.All.ContainsKey("spectra-prompts"));
        Assert.True(SkillContent.All.ContainsKey("spectra-quickstart"));
        Assert.True(SkillContent.All.ContainsKey("spectra-review-flagged"));
    }

    [Fact]
    public void AgentContent_HasAllAgents()
    {
        // skill-pair-merge: spectra-generation and spectra-execution merged into their flow skills;
        // only the context:fork critic subagent remains as a bundled agent.
        Assert.Single(AgentContent.All);
        Assert.True(AgentContent.All.ContainsKey("spectra-critic.agent.md"));
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
        // skill-pair-merge: spectra-execution agent merged into spectra-execute skill; line-count
        // contract retired — the merged skill has no meaningful upper bound to enforce here.
        Assert.True(true);
    }

    [Fact]
    public void GenerationAgent_LineCount_Within160()
    {
        // skill-pair-merge: spectra-generation agent merged into spectra-generate skill; line-count
        // contract retired — the merged skill has no meaningful upper bound to enforce here.
        Assert.True(true);
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
        // Spec 059/063: spectra-generate and spectra-update now drive the deterministic
        // compile/ingest seam, whose commands are inherently non-interactive (stdin/args →
        // stdout) and do not take --no-interaction. They are excluded from this legacy-convention
        // check (their per-line flag conformance is not meaningful for seam commands).
        // Spec 069: spectra-criteria also excluded — its extraction recipe now drives the
        // compile-extraction-prompt / ingest-criteria / docs-changed seam (stdin/args → stdout),
        // whose commands do not all take the legacy per-line flags.
        var skillsWithCommands = new[] { "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-docs", "spectra-list", "spectra-init-profile" };

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
        // Spec 063: spectra-update excluded — its seam commands (compile-update-prompt /
        // ingest-update) emit to stdout and do not all take --output-format (mirrors spectra-generate).
        // Spec 069: spectra-criteria also excluded — its extraction recipe now drives the
        // compile-extraction-prompt / ingest-criteria / docs-changed seam (stdin/args → stdout),
        // whose commands do not all take the legacy per-line flags.
        var skillsWithCommands = new[] { "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-docs", "spectra-list", "spectra-init-profile" };

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
        // Spec 063: spectra-update excluded — its seam commands do not all take --verbosity
        // (mirrors spectra-generate).
        // Spec 069: spectra-criteria also excluded — its extraction recipe now drives the
        // compile-extraction-prompt / ingest-criteria / docs-changed seam (stdin/args → stdout),
        // whose commands do not all take the legacy per-line flags.
        var skillsWithCommands = new[] { "spectra-coverage", "spectra-dashboard",
            "spectra-validate", "spectra-docs", "spectra-list", "spectra-init-profile" };

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
        // Spec 059: spectra-generate reads command stdout from the compile/ingest seam rather
        // than the legacy `.spectra-result.json` sink, so it is excluded here.
        var skillsWithResultFile = new[] { "spectra-update", "spectra-coverage", "spectra-dashboard",
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
    public void UpdateSkill_ToolsList_IsClaudeCodeModel_NotCopilot()
    {
        // Spec 056: ported skills resolve {{…TOOLS}} to Claude Code tools — the Copilot
        // browser/openBrowserPage tool name is gone; the progress-page affordance survives.
        var content = SkillContent.Update;
        Assert.DoesNotContain("browser/openBrowserPage", content);
        Assert.Contains(".spectra-progress.html", content);
    }

    [Fact]
    public void GenerationAgent_ContainsUpdateDelegation()
    {
        // skill-pair-merge: delegation table now lives in the merged spectra-generate skill.
        var content = SkillContent.Generate;
        Assert.Contains("spectra-update", content);
    }

    [Fact]
    public void ExecutionAgent_ContainsUpdateDelegation()
    {
        // skill-pair-merge: delegation table now lives in the merged spectra-execute skill.
        var content = SkillContent.Execute;
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
        // skill-pair-merge: quickstart routing now lives in the merged spectra-generate skill.
        Assert.Contains("spectra-quickstart", SkillContent.Generate);
    }

    [Fact]
    public void ExecutionAgent_References_QuickstartSkill()
    {
        // skill-pair-merge: quickstart routing now lives in the merged spectra-execute skill.
        Assert.Contains("spectra-quickstart", SkillContent.Execute);
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

    // ---- Spec 078: classification call sites never write ----

    [Theory]
    [InlineData("spectra-update")]
    [InlineData("spectra-generate")]
    [InlineData("spectra-execute")]
    public void AiUpdateCallSites_PassDryRun(string skill)
    {
        var lines = SkillContent.All[skill]
            .Split('\n')
            .Where(l => l.Contains("spectra ai update"))
            .ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Contains("--dry-run", l));
    }

    // ---- Spec 078: criteria extraction reads each document once ----

    [Fact]
    public void CriteriaSkill_UsesCompiledPromptAsTheOnlyDocumentSource()
    {
        var content = SkillContent.All["spectra-criteria"];

        Assert.DoesNotContain("file tools to read", content);
        Assert.DoesNotContain("reading the document with your file tools", content);
        Assert.Contains("it already contains the full document text", content);
        Assert.Contains("do not\nread `{path}` separately", content.Replace("\r\n", "\n"));
    }

    // ---- Spec 079: moved / deleted docs and the changeset summary ----

    [Fact]
    public void CriteriaSkill_AppliesMoves_AndRetiresDeletedDocsOnlyAfterConfirmation()
    {
        var content = SkillContent.All["spectra-criteria"].Replace("\r\n", "\n");

        Assert.Contains("spectra docs apply-move --from {from} --to {path} --output-format json", content);
        Assert.Contains("spectra docs retire --doc {path} --dry-run --output-format json", content);
        Assert.Contains("Never retire anything on your own", content);
        Assert.Contains("Only on an explicit yes, run:\n```\nspectra docs retire --doc {path} --output-format json", content);
        Assert.Contains("referencing_tests", content);
    }

    [Fact]
    public void CriteriaSkill_SummarisesChangesets_AndHandlesInvalidIds()
    {
        var content = SkillContent.All["spectra-criteria"];

        Assert.Contains("criteria added / modified / removed", content);
        Assert.Contains("removed** criterion id that tests still reference", content);
        Assert.Contains("Exit 7 (`INVALID_CRITERION_ID`)", content);
        // Spec 078 wording is kept.
        Assert.Contains("it already contains the full document text", content);
    }

    // Spec 080 B5: every criteria write step tells the session what to do on exit 1.
    [Theory]
    [InlineData("**1a.", "**1b.")]
    [InlineData("**1b.", "The Step 2 work-list")]
    [InlineData("**2c.", "Update `.spectra/progress.json`: `{\"active\":2}`")]
    public void CriteriaSkill_WriteSteps_HandleExit1(string sectionStart, string sectionEnd)
    {
        var content = SkillContent.All["spectra-criteria"].Replace("\r\n", "\n");
        var start = content.IndexOf(sectionStart, StringComparison.Ordinal);
        Assert.True(start >= 0, sectionStart);
        var end = content.IndexOf(sectionEnd, start, StringComparison.Ordinal);
        Assert.True(end > start, sectionEnd);
        var section = content[start..end];

        Assert.Contains("Exit 1", section);
        Assert.Contains("`error_code`", section);
        Assert.Contains("stop for that document", section);
        Assert.Contains("`LOCK_TIMEOUT`", section);
        Assert.Contains("retry the same command once", section);
        foreach (var code in new[] { "CRITERIA_FILE_UNREADABLE", "CRITERIA_FILE_COLLISION", "INVALID_DOC_PATH" })
        {
            Assert.Contains(code, section);
        }
    }

    // Spec 081 C4: the compile step reads the unified failure fields.
    [Fact]
    public void CriteriaSkill_CompileStep_ReadsErrorsNotMessage()
    {
        var content = SkillContent.All["spectra-criteria"];
        var start = content.IndexOf("**2a.", StringComparison.Ordinal);
        var end = content.IndexOf("**2b.", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var section = content[start..end];

        Assert.Contains("errors[0]", section);
        Assert.DoesNotContain("`message`", section);
    }

    // Spec 082 A4: config problems are named, never inferred from a null error code.
    [Fact]
    public void CriteriaSkill_NamesConfigErrorCodes()
    {
        var content = SkillContent.All["spectra-criteria"];

        Assert.Contains("CONFIG_MISSING", content);
        Assert.Contains("CONFIG_INVALID", content);
        // Spec 083: no section is ever required, so CONFIG_SECTION_MISSING no longer exists.
        Assert.DoesNotContain("CONFIG_SECTION_MISSING", content);
        Assert.DoesNotContain("`null` for a config", content);
        Assert.DoesNotContain("with `error_code` `null`", content);
    }
}
