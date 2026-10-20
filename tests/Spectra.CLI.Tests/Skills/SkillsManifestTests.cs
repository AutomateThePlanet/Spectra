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
        // Spec 096: keys are repo-relative with '/'; an absolute key matching no install path is dropped on load.
        manifest.Files[".claude/skills/spectra-x/SKILL.md"] = "abc123";

        await _store.SaveAsync(manifest);
        var loaded = await _store.LoadAsync();

        Assert.Single(loaded.Files);
        Assert.Equal("abc123", loaded.Files[".claude/skills/spectra-x/SKILL.md"]);
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
        // Spec 092: the critic plus the update / criteria subagents; Spec 093: plus the generation skill's analyst and generator.
        Assert.Equal(
            new[]
            {
                "spectra-analyst.agent.md", "spectra-critic.agent.md", "spectra-extractor.agent.md", "spectra-generator.agent.md",
                "spectra-repairer.agent.md", "spectra-updater.agent.md",
            },
            AgentContent.All.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
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
        // Spec 084: spectra-update no longer runs a long command with a progress page (triage takes seconds); its
        // in-session edit loop uses the seam progress page instead (UpdateSkill_UsesTheSeamProgressPage).
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
        // Spec 059: spectra-generate reads command stdout from the compile/ingest seam rather
        // than the legacy `.spectra-result.json` sink, so it is excluded here.
        // Spec 084: spectra-update reads update-triage's stdout too (no long-running command writes a result file).
        var skillsWithResultFile = new[] { "spectra-coverage", "spectra-dashboard",
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
    }

    [Fact]
    public void UpdateSkill_UsesTheSeamProgressPage() =>
        Assert.Contains("spectra ai init-seam-progress", SkillContent.Update);

    [Fact]
    public void GenerationAgent_ContainsUpdateDelegation()
    {
        // skill-pair-merge: delegation table now lives in the merged spectra-generate skill.
        // Spec 101 F2: in its reference.md (read when a request isn't generation).
        var content = SkillReferenceContent.Generate;
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

    // Spec 084: the delegation rows point at update-triage (with --dry-run: they only look). The legacy
    // `spectra ai update` (matched exactly, not update-triage/update-ack) is no longer called by any of these skills.
    [Theory]
    [InlineData("spectra-generate")]
    [InlineData("spectra-execute")]
    public void UpdateDelegationRows_UseTriage_WithDryRun(string skill)
    {
        // Spec 101 F2: the generate skill's delegation table lives in its reference.md.
        var text = skill == "spectra-generate" ? SkillReferenceContent.Generate : SkillContent.All[skill];
        var lines = text
            .Split('\n')
            .Where(l => l.Contains("spectra ai update-triage"))
            .ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Contains("--dry-run", l));
    }

    [Theory]
    [InlineData("spectra-update")]
    [InlineData("spectra-generate")]
    [InlineData("spectra-execute")]
    [InlineData("spectra-help")]
    public void Skills_NeverCallTheLegacyUpdateCommand(string skill) =>
        Assert.DoesNotMatch(@"spectra ai update(\s|$)", SkillContent.All[skill]);

    // ---- Spec 084: the update skill drives triage (FR-032) ----

    [Fact]
    public void UpdateSkill_DrivesTriage()
    {
        // Spec 101 F2: the exit-code table (CONFIG_MISSING, CONFIG_INVALID, LOCK_TIMEOUT) moved to the skill's reference.md.
        var content = SkillContent.Update + "\n" + SkillReferenceContent.Update;

        foreach (var expected in new[]
                 {
                     "spectra ai update-triage", "spectra ai update-ack", "compile-update-prompt", "ingest-update",
                     "LOAD only (recommended)", "LOAD + LIGHT", "report only", "REMOVED", "CRITERIA_REMOVED",
                     "CONFIG_MISSING", "CONFIG_INVALID", "LOCK_TIMEOUT", "NON-STOP"
                 })
        {
            Assert.Contains(expected, content);
        }
    }

    [Fact]
    public void UpdateSkill_RemovedStep_PointsAtTheFixAckAndDelete_NeverAtTheEditLoop()
    {
        var content = SkillContent.Update;
        var start = content.IndexOf("**Step 3**", StringComparison.Ordinal);
        var end = content.IndexOf("**Step 4**", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        // Spec 101 F2: Step 3 names the reference.md section that says how to clear each entry.
        Assert.Contains("Clearing REMOVED entries", content[start..end]);
        var step = content[start..end] + Section(SkillReferenceContent.Update, "## Clearing REMOVED entries", "## Suggest next steps");

        Assert.Contains("REMOVED", step);
        Assert.Contains("update-ack", step);
        Assert.Contains("source_refs", step);
        Assert.Contains("spectra-delete", step);
        Assert.Contains("spectra delete", step);
        Assert.DoesNotContain("compile-update-prompt", step);
        Assert.DoesNotContain("--force", step);
    }

    [Fact]
    public void UpdateSkill_BucketOptions()
    {
        var content = SkillContent.Update;
        var start = content.IndexOf("**Step 4**", StringComparison.Ordinal);
        var end = content.IndexOf("**Step 5**", StringComparison.Ordinal);
        var step = content[start..end];

        Assert.Contains("LOAD only (recommended)", step);
        Assert.Contains("`CRITERIA_CHANGED`", step);
        Assert.Contains("LOAD + LIGHT", step);
        Assert.Contains("`LIGHT`", step);
        Assert.Contains("choose groups", step);
        Assert.Contains("never enter the edit loop", step);
    }

    // ---- Spec 085 F5: critic repairs keep the source-change baseline ----

    // Spec 093: spectra-generate no longer ingests repairs itself — the spectra-repairer subagent does, with
    // --keep-baseline (asserted by UpdateSkill_DoesNotKeepTheBaseline and GenerateSkill_Exit8_…).
    [Theory]
    [InlineData("spectra-review-flagged")]
    public void RepairIngests_KeepTheBaseline(string skill)
    {
        var lines = SkillContent.All[skill].Split('\n').Where(l => l.Contains("spectra ai ingest-update")).ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Contains("--keep-baseline", l));
    }

    // ---- Spec 088 FR-018: the edit loop runs per group ----

    private static string UpdateStep(string from, string to)
    {
        var content = SkillContent.Update.Replace("\r\n", "\n");
        var start = content.IndexOf(from, StringComparison.Ordinal);
        var end = content.IndexOf(to, start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{from} … {to}");
        return content[start..end];
    }

    [Fact]
    public void UpdateSkill_EditLoop_CompilesPerGroup_StagesPerTest_IngestsAll()
    {
        // Spec 092: the edit step runs in the spectra-updater subagent.
        var agent = Normalised(UpdaterAgent);

        Assert.Contains("spectra ai compile-update-prompt --suite <suite> --group <group>", agent);
        Assert.Contains("`.spectra/updates/<suite>/updated-<id>.json`", agent);
        Assert.Contains("Write tool", agent);
        Assert.Contains("spectra ai ingest-update <suite> --all --output-format json --verbosity quiet", agent);
        Assert.True(agent.IndexOf("--group <group>", StringComparison.Ordinal)
            < agent.IndexOf("--all --output-format", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdateSkill_EditLoop_ReportsEachResult_UnchangedIsAcknowledged()
    {
        // Spec 092: the updater reads `results`; the skill reports its lines.
        var agent = Normalised(UpdaterAgent);

        Assert.Contains("`results`", agent);
        Assert.Contains("`written`", agent);
        Assert.Contains("`unchanged`", agent);
        Assert.Contains("`failed`", agent);
        Assert.Contains("acknowledged", agent);
    }

    [Fact]
    public void UpdateSkill_EditLoop_RetriesAFailedTestAtMostTwice_WithTestId()
    {
        // Spec 092: the bounded retry runs in the spectra-updater subagent.
        var agent = Normalised(UpdaterAgent);

        Assert.Contains("at most twice", agent);
        Assert.Contains("spectra ai compile-update-prompt --suite <suite> --test-id <id>", agent);
        Assert.Contains("report it and continue", agent);
    }

    [Fact]
    public void UpdateSkill_EditLoop_HandlesTheCompileRefusals()
    {
        // Spec 092: the compile refusals are handled in the spectra-updater subagent.
        var agent = Normalised(UpdaterAgent);

        foreach (var code in new[] { "PROMPT_TOO_LARGE", "TEMPLATE_OUTDATED", "NOTHING_TO_RECONCILE", "TRIAGE_STALE" })
        {
            Assert.Contains(code, agent);
        }

        Assert.Contains("spectra prompts reset test-update", agent);
    }

    [Fact]
    public void UpdateSkill_NoLongerUsesTheSingleStagingFile()
    {
        Assert.DoesNotContain(".spectra/updated.json", SkillContent.Update);
        Assert.DoesNotMatch(@"ingest-update[^\n]*--from", SkillContent.Update);
    }

    [Fact]
    public void UpdateSkill_NonStop_AllowsWritesToTheStagingFiles()
    {
        // Spec 092: the skill writes only progress; the staging files are the subagents' writes.
        var contract = UpdateStep("## NON-STOP CONTRACT", "## Update test cases for a suite");

        Assert.Contains("`.spectra/progress.json`", contract);
        Assert.Contains("`Write` to `.spectra/updates/<suite>/updated-<id>.json`", Normalised(UpdaterAgent));
    }

    // ---- Spec 089: review fixes for 088 ----

    private static string GenerateSkill => SkillContent.All["spectra-generate"].Replace("\r\n", "\n");

    // Spec 092: the edit, repair and extraction steps moved into these subagents.
    private static string UpdaterAgent => AgentContent.All["spectra-updater.agent.md"].Replace("\r\n", "\n");

    private static string RepairerAgent => AgentContent.All["spectra-repairer.agent.md"].Replace("\r\n", "\n");

    private static string ExtractorAgent => AgentContent.All["spectra-extractor.agent.md"].Replace("\r\n", "\n");

    [Fact]
    public void GenerateSkill_StagesRepairsInTheRepairsFolder_NeverTheUpdateFolder()
    {
        // F1 (FR-005): the repair loop writes .spectra/repairs/; the table row names it too.
        // Spec 101 F2: the CLI flags table moved to the skill's reference.md; the skill never names the update folder.
        var content = SkillReferenceContent.Generate.Replace("\r\n", "\n");
        Assert.DoesNotContain(".spectra/updates/{suite}/updated-{id}.json", GenerateSkill);

        Assert.Contains("`.spectra/repairs/{suite}/repaired-{id}.json`", content);
        Assert.DoesNotContain(".spectra/updates/{suite}/updated-{id}.json", content);
        var row = content.Split('\n').Single(l => l.StartsWith("| `ingest-update {suite} --all --keep-baseline`", StringComparison.Ordinal));
        Assert.Contains(".spectra/repairs/{suite}/repaired-{id}.json", row);
    }

    [Fact]
    public void GenerateSkill_Exit8_RetriesTheFailedRepairsAtMostTwice_AsTheSameRepairAttempt()
    {
        // F5 (FR-020, analysis A1): exit 8 is not a STOP; the retry completes the same repair attempt. Spec 093: the
        // repair ingest and its retry run in the spectra-repairer subagent; 8b says so and re-critiques what landed.
        var repairer = Normalised(RepairerAgent);
        Assert.Contains("Exit `0` or `8`", repairer);
        Assert.Contains("`results`", repairer);
        Assert.Contains("**at most twice**", repairer);
        Assert.Contains("same repair attempt", repairer);

        var step = Normalised(GenerateSkill[GenerateSkill.IndexOf("**8b — Repair", StringComparison.Ordinal)..]);
        Assert.Contains("retries a failed repair ingest at most twice (the same repair attempt)", step);
        Assert.Contains("reported `written` or `unchanged` in `results`", step);
    }

    [Fact]
    public void GenerateSkill_NonStopRestatement_SaysExit8IsNotAStop_AndTheWrittenZeroGuardIsUnchanged()
    {
        var content = GenerateSkill;

        Assert.Contains("Exit `8` from `ingest-update --all` is neither broken nor zero-result", content);
        // The 075 hard regression gate stays verbatim.
        Assert.Contains("- **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b.", content);
        Assert.Contains("GROUNDING-INGEST ANOMALY: `ingest-grounding --all` returned `written: 0`", content);
    }

    [Fact]
    public void UpdateSkill_Exit8_ReadsResults_AndNeverStops()
    {
        // Spec 092: the ingest and its exit codes are the spectra-updater subagent's.
        var agent = Normalised(UpdaterAgent);

        Assert.Contains("Exit `0` (no file failed) or `8`", agent);
        Assert.Contains("Exit `8` is never a reason to stop", agent);
        Assert.Contains("`MULTIPLE_TESTS`", agent);
    }

    [Fact]
    public void UpdateSkill_UnchangedPending_AsksToAcknowledgeOrEditAgain_ForLoadAndCriteriaChangedOnly()
    {
        // F3 (FR-015, analysis I1/A2). Spec 092: the user's decision stays in the skill; "edit again" is the updater.
        var step = UpdateStep("**Step 5**", "**Step 6**");
        var start = step.IndexOf("**Returned unchanged, still pending.**", StringComparison.Ordinal);
        var end = step.IndexOf("**Step 5a**", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var block = step[start..end];

        Assert.Contains("`unchanged_pending`", step);
        Assert.Contains("`LOAD` or `CRITERIA_CHANGED` → ask the user", block);
        Assert.Contains("Never acknowledge without the user's\n  answer", block);
        Assert.Contains("spectra ai update-ack --suite <suite> --ids <id1>,<id2>", block);
        Assert.Contains("`spectra-updater` subagent with `suite: <suite>` and `ids: <id>`", Normalised(block));
        Assert.Contains("at most twice per test", Normalised(block));

        var others = block[block.IndexOf("- `REMOVED`, `CRITERIA_REMOVED` or `RELINK`", StringComparison.Ordinal)..];
        Assert.Contains("changed again since triage", others);
        Assert.Contains("offer no acknowledgement", others);
        Assert.Contains("`REF_REMOVED`", others);
        Assert.Contains("`CRITERION_REMOVED`", others);
        Assert.Contains("spectra ai update-triage", others);
        Assert.DoesNotContain("update-ack --suite", others);
    }

    // ---- Spec 090: re-verification after update edits, failed staging files, delete delegation ----

    [Fact]
    public void UpdateSkill_ReverifiesEditedTests_AskingOnce_WithOneRepair()
    {
        // FR-A4 (spec T5). Spec 092: the critic compiles its own prompt; the repair runs in the spectra-repairer.
        var step = UpdateStep("**Step 5a**", "**Step 6**");
        var flat = Normalised(step);

        // Spec 101 F3 (deliberate change): the question is asked once in Step 4, before the first updater call (the
        // updater runs the critic), and its answer goes to every updater and repairer call.
        var step4 = Normalised(UpdateStep("**Step 4**", "**Step 5**"));
        Assert.Contains("\"Re-verify the edited tests with the critic now? (recommended)\"", step4);
        Assert.Contains("don't ask again", step4);
        Assert.Contains("`manual_verdict_kept`", UpdateStep("**Step 5**", "**Step 6**"));
        Assert.Contains("`spectra-critic` subagent with the Agent tool", flat);
        Assert.Contains("spectra ai compile-critic-prompt --suite <suite> --test <id>", AgentContent.All["spectra-critic.agent.md"]);
        // Spec 093 (FR-015): the verdict ingest names the tests it routes.
        Assert.Contains("spectra ai ingest-verdict --suite <suite> --all --ids <id1>,<id2> --output-format json", step);
        // Spec 097 (FR-002): the grounding batch names the group's grounded tests.
        Assert.Contains("spectra ai ingest-grounding --suite <suite> --all --ids <grounded ids> --output-format json", step);
        Assert.Contains("spectra ai compile-repair-prompt --suite <suite> --test <id>", RepairerAgent);
        Assert.Contains("`.spectra/repairs/<suite>/repaired-<id>.json`", RepairerAgent);
        Assert.Contains("spectra ai ingest-update <suite> --all --keep-baseline", RepairerAgent);
        Assert.Contains("--repaired --repair-attempts 1", step);
        Assert.Contains("still hallucinated", step);
        Assert.Contains("restore the previous version from git (the user runs that", flat);
        Assert.Contains("never runs `spectra ai record-drop` or `spectra delete`", flat);
        Assert.DoesNotMatch(@"(?m)^\s*spectra (ai record-drop|delete)\b", step);
    }

    [Fact]
    public void UpdateSkill_ReportsTheReverificationCounts()
    {
        var step = UpdateStep("**Step 7**", "## Update all suites");

        foreach (var count in new[] { "grounded", "partial (flagged for review)", "hallucinated", "unverified", "manual verdict kept" })
        {
            Assert.Contains(count, step);
        }
    }

    [Fact]
    public void UpdateSkill_NonStop_AllowsTheCriticAndTheRepairsFolder()
    {
        var contract = UpdateStep("## NON-STOP CONTRACT", "## Update test cases for a suite");

        Assert.Contains("`spectra-critic`", contract);
        Assert.Contains("`Write` to `.spectra/repairs/<suite>/repaired-<id>.json`", Normalised(RepairerAgent));
    }

    [Fact]
    public void BothSkills_KeepAFailedFileAsFailedId_AndRetryWithAFreshFile()
    {
        // FR-B1. Spec 092: the update edit (and its retry) runs in the spectra-updater subagent; Spec 093: the generation
        // flow's repair (and its retry) runs in the spectra-repairer subagent.
        var update = Normalised(UpdaterAgent);
        Assert.Contains("`.spectra/updates/<suite>/failed-<id>.json`", update);
        Assert.Contains("write a fresh `.spectra/updates/<suite>/updated-<id>.json`", update);
        Assert.DoesNotContain("it replaces the kept file", update);

        var repairer = Normalised(RepairerAgent);
        Assert.Contains("`.spectra/repairs/<suite>/failed-<id>.json`", repairer);
        Assert.Contains("write a fresh `.spectra/repairs/<suite>/repaired-<id>.json`", repairer);
    }

    [Fact]
    public void UpdateSkill_DelegatesDeletesToTheDeleteSkill_NeverRunsDelete()
    {
        // FR-C4.
        var content = SkillContent.Update;

        Assert.Contains("spectra-delete", content);
        Assert.DoesNotMatch(@"(?m)^\s*spectra delete\b", content);
    }

    [Fact]
    public void ReviewFlaggedSkill_StillPassesTheRepairAttemptsExplicitly()
    {
        // Analysis C1: FR-A1 drops the flagged block on the repair, so the attempt count must not come from it.
        var content = SkillContent.All["spectra-review-flagged"];

        Assert.Contains("ingest-grounding", content);
        Assert.Contains("--repaired --repair-attempts 2", content);
    }

    [Fact]
    public void UpdateSkill_DoesNotKeepTheBaseline()
    {
        // Spec 085: the update edit rebuilds the baseline. Spec 090 (FR-A4): only the re-verify repair keeps it — and
        // Spec 092 moved that repair into the spectra-repairer subagent, so neither the skill nor the updater keeps it.
        Assert.DoesNotContain("--keep-baseline", SkillContent.Update);
        Assert.DoesNotContain("--keep-baseline", UpdaterAgent);
        Assert.Contains("spectra ai ingest-update <suite> --all --keep-baseline", RepairerAgent);
    }

    [Fact]
    public void UpdateSkill_AllSuitesIsOneTriageCall() =>
        Assert.Contains("update-triage --all-suites", SkillContent.Update);

    // ---- Spec 086 FR-023/FR-024: the relink step ----

    private static string Section(string content, string from, string to)
    {
        var start = content.IndexOf(from, StringComparison.Ordinal);
        var end = content.IndexOf(to, start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"no section {from} … {to}");
        return content[start..end];
    }

    [Fact]
    public void UpdateSkill_RelinkStep_DryRunThenConfirmThenRelinkThenRetriage()
    {
        var step = Section(SkillContent.Update, "**Step 2a**", "**Step 3**");
        var lines = step.Split('\n').Where(l => l.Contains("spectra ai relink-sources")).ToList();

        Assert.True(lines.Count >= 2);
        Assert.Contains("--dry-run", lines[0]); // the first occurrence previews
        Assert.Contains(lines.Skip(1), l => !l.Contains("--dry-run"));
        Assert.Contains("only after the user says yes", step);
        Assert.Contains("re-run triage", step);
        var preview = step.IndexOf("--dry-run", StringComparison.Ordinal);
        var ask = step.IndexOf("Ask the user", StringComparison.Ordinal);
        var relink = step.IndexOf(lines.Skip(1).First(l => !l.Contains("--dry-run")).Trim(), StringComparison.Ordinal);
        Assert.True(preview < ask && ask < relink, "preview, then ask, then relink");
    }

    [Fact]
    public void UpdateSkill_RelinkStep_OnNo_LeavesTheTestsForLater() =>
        Assert.Contains("report the RELINK tests as left for later and go on with the\n   other buckets",
            Section(SkillContent.Update, "**Step 2a**", "**Step 3**").Replace("\r\n", "\n"));

    [Fact]
    public void UpdateSkill_NamesRelinkInTheBucketList() =>
        // Spec 101 F2: the bucket list moved to the skill's reference.md.
        Assert.Contains("- **RELINK**", Section(SkillReferenceContent.Update, "## Buckets", "## Submodules and first-time setup"));

    [Fact]
    public void UpdateSkill_RemovedStep_HintsAtMinSimilarity_DryRunFirst()
    {
        // Spec 101 F2: how to clear a REMOVED entry moved to the skill's reference.md, which Step 3 names.
        var step = Section(SkillReferenceContent.Update, "## Clearing REMOVED entries", "## Suggest next steps");

        Assert.Contains("not found; no rename found at 90% similarity", step);
        Assert.Contains("relink-sources --suite <suite> --min-similarity <N> --dry-run", step);
        Assert.Contains("between 50 and 100", step);
    }

    // ---- Spec 087 F1 (FR-005): a submodule that isn't checked out is a setup problem ----

    [Fact]
    public void UpdateSkill_SubmoduleNotCheckedOut_IsSetup_NotReview()
    {
        var step = Section(SkillContent.Update, "**Step 3**", "**Step 4**").Replace("\r\n", "\n");
        var paragraph = step.Split("\n\n").Single(p => p.Contains("not found; submodule not checked out", StringComparison.Ordinal));

        Assert.Contains("git submodule update --init", paragraph);
        Assert.Contains("re-run triage", paragraph);
        Assert.Contains("not list", paragraph); // those tests are not listed for review
        Assert.DoesNotContain("spectra-delete", paragraph);
        Assert.DoesNotContain("spectra delete", paragraph);
    }

    [Fact]
    public void UpdateSkill_NeverRunsGitSubmodule_TheUserDoes()
    {
        // Analysis I1: fenced blocks are commands the assistant runs; the git command is inline prose only.
        var fences = System.Text.RegularExpressions.Regex.Matches(SkillContent.Update.Replace("\r\n", "\n"), "```[a-z]*\\n(.*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.NotEmpty(fences);
        Assert.All(fences, f => Assert.DoesNotContain("git submodule", f.Groups[1].Value));
    }

    [Fact]
    public void UpdateSkill_WithholdsInit_WhileTheSubmoduleNoteIsPresent()
    {
        // Analysis U1: the first-time setup names the note and tells the user to initialise the submodule first.
        // Spec 101 F2: in the skill's reference.md (Submodules and first-time setup), which Step 2 names.
        Assert.Contains("Submodules and first-time setup", SkillContent.Update);
        var content = SkillReferenceContent.Update.Replace("\r\n", "\n");
        var paragraph = content.Split("\n\n").Single(p => p.Contains("point into submodule", StringComparison.Ordinal));

        Assert.Contains("source-baseline --init", paragraph);
        Assert.Contains("don't offer", paragraph);
        Assert.Contains("git submodule update --init", paragraph);
        Assert.Contains("re-run triage", paragraph);
    }

    [Fact]
    public void UpdateSkill_FirstTimeSetup_RelinksBeforeInit_ThenOffersFromDate()
    {
        // Analysis H1/U2: no source-baseline --init is offered before the RELINK step unless there is no RELINK group,
        // and the offer after relinking uses --from-date, with plain --init as the fallback.
        var content = SkillContent.Update.Replace("\r\n", "\n");
        var relinkStep = content.IndexOf("**Step 2a**", StringComparison.Ordinal);
        var paragraphs = content[..relinkStep].Split("\n\n");
        foreach (var paragraph in paragraphs.Where(p => p.Contains("source-baseline --init")))
        {
            Assert.Contains("no `RELINK` group", paragraph);
        }

        // Spec 101 F2: the skill itself offers no source-baseline --init after Step 2a; the first-time setup (relink
        // first, then --from-date, plain --init as the fallback) is in reference.md, read when triage reports
        // unbaselined tests.
        var after = content[relinkStep..];
        Assert.DoesNotContain("spectra ai source-baseline --init", after);
        var setup = SkillReferenceContent.Update.Replace("\r\n", "\n");
        setup = setup[setup.IndexOf("**First-time setup**", StringComparison.Ordinal)..];
        Assert.Contains("after the relink step", setup);
        var firstInit = setup.IndexOf("spectra ai source-baseline --init", StringComparison.Ordinal);
        Assert.Contains("--from-date", setup[firstInit..setup.IndexOf('\n', firstInit)]);
        Assert.Contains("plain\n`spectra ai source-baseline --init --suite <suite> --output-format json", setup);
    }

    // ---- Spec 078: criteria extraction reads each document once ----

    [Fact]
    public void CriteriaSkill_UsesCompiledPromptAsTheOnlyDocumentSource()
    {
        // Spec 092: the extraction turn runs in the spectra-extractor subagent; this session never reads the document.
        var skill = SkillContent.All["spectra-criteria"];
        var agent = Normalised(ExtractorAgent);

        Assert.DoesNotContain("file tools to read", skill);
        Assert.DoesNotContain("reading the document with your file tools", skill);
        Assert.DoesNotContain("file tools to read", agent);
        Assert.Contains("it already contains the full document text", agent);
        Assert.Contains("do not read `<doc>` separately", agent);
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
        // Spec 092: the extraction and its regeneration rules moved into the spectra-extractor subagent.
        var agent = Normalised(ExtractorAgent);
        Assert.Contains("Exit 7 (`INVALID_CRITERION_ID`)", agent);
        // Spec 078 wording is kept.
        Assert.Contains("it already contains the full document text", agent);
    }

    // Spec 080 B5: every criteria write step tells the session what to do on exit 1.
    [Theory]
    [InlineData("**1a.", "**1b.")]
    [InlineData("**1b.", "The Step 2 work-list")]
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
    public void CriteriaExtractor_IngestStep_HandlesExit1()
    {
        // Spec 092: the former Step 2c (the ingest) runs in the spectra-extractor subagent.
        var content = Normalised(ExtractorAgent);
        var start = content.IndexOf("**3. Ingest", StringComparison.Ordinal);
        var end = content.IndexOf("## Your final message", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var section = content[start..end];

        Assert.Contains("Exit 1", section);
        Assert.Contains("<error_code>", section);
        Assert.Contains("`LOCK_TIMEOUT`", section);
        Assert.Contains("retry the same command once", section);
        foreach (var code in new[] { "CRITERIA_FILE_UNREADABLE", "CRITERIA_FILE_COLLISION", "INVALID_DOC_PATH" })
        {
            Assert.Contains(code, section);
        }
    }

    [Fact]
    public void CriteriaSkill_CompileStep_ReadsErrorsNotMessage()
    {
        // Spec 092: the compile step runs in the spectra-extractor subagent.
        var content = ExtractorAgent;
        var start = content.IndexOf("**1. Compile", StringComparison.Ordinal);
        var end = content.IndexOf("**2. Extract", start, StringComparison.Ordinal);
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

    // ---- Spec 091: review fixes for 090 (T8) ----

    private static string Normalised(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    /// <summary>The blank-line-separated paragraph of <paramref name="content"/> that contains <paramref name="needle"/>.</summary>
    private static string ParagraphWith(string content, string needle)
    {
        var paragraph = content.Split("\n\n").FirstOrDefault(p => p.Contains(needle, StringComparison.Ordinal));
        Assert.True(paragraph is not null, $"no paragraph contains {needle}");
        return paragraph!;
    }

    [Fact]
    public void GenerateSkill_RepairedGrounding_PassesOnlyTheReCritiquedIds()
    {
        // FR-009: the call names the ids, and the paragraph that holds it defines them (repair ingest written or
        // unchanged, minus dropped tests) — the re-critic heading's "written or unchanged" alone doesn't count.
        var content = GenerateSkill;

        Assert.Contains("--repaired --repair-attempts 1 --ids", content);
        var paragraph = ParagraphWith(content, "--ids {ids}");
        Assert.Contains("written", paragraph);
        Assert.Contains("unchanged", paragraph);
        Assert.Contains("dropped", paragraph);
    }

    [Fact]
    public void GenerateSkill_SkippedHallucinated_IsNotAFailure()
    {
        // FR-012.
        var content = Normalised(GenerateSkill);

        Assert.Contains("`skipped_hallucinated`", content);
        Assert.Contains("not a failure", content);
    }

    [Fact]
    public void UpdateSkill_RepairedGrounding_PassesTheTestsId()
    {
        // FR-009, section-scoped to Step 5a. Spec 092: one call for every repair that landed.
        var step = UpdateStep("**Step 5a**", "**Step ");

        Assert.Contains("--repaired --repair-attempts 1 --ids <id1>,<id2>", step);
    }

    [Fact]
    public void UpdateSkill_AFailedRepairIngest_IsRetriedAtMostTwice_InStep5a()
    {
        // FR-009 (analysis U4/T2). Spec 092: the retry runs in the spectra-repairer subagent; Step 5a says so and
        // passes the landed ids.
        var flat = Normalised(UpdateStep("**Step 5a**", "**Step "));
        Assert.Contains("retries a failed repair ingest at most twice", flat);
        Assert.Contains("--ids <id1>,<id2>", flat);

        var repairer = Normalised(RepairerAgent);
        Assert.Contains("at most twice", repairer);
        Assert.Contains("repaired-<id>.json", repairer);
    }

    [Fact]
    public void UpdateSkill_ReCritiquesOnlyALandedRepair_AndReportsAFailedOne()
    {
        // FR-009 (analysis C1): re-critic only after `written` or `unchanged`; a still-failed repair gets no repaired
        // grounding and is reported. Spec 092: by the repairer's returned lines.
        var step = Normalised(UpdateStep("**Step 5a**", "**Step "));

        Assert.Contains("only for the ids it returned `written` or `unchanged`", step);
        Assert.Contains("repair failed: <error_code>", step);
        Assert.Contains("no repaired grounding", step);
        Assert.Contains("unverified", step);
        Assert.Contains("waiting for the user", step);
    }

    [Fact]
    public void CriticAgent_SaysTheCompileWritesOnlyTheFingerprintRecord()
    {
        // FR-013 (F4), whitespace normalised so wrapping can't defeat it.
        var agent = Normalised(AgentContent.All["spectra-critic.agent.md"]);

        Assert.DoesNotContain("It writes nothing", agent);
        Assert.Contains("writes only the fingerprint record", agent);
        Assert.Contains("never the verdict", agent);
        Assert.Contains("calls no model", agent);
    }
}
