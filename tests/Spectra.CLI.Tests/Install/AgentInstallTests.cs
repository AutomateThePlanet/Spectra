using System.Text.Json.Nodes;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Install;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Install;

/// <summary>
/// Spec 104 (SC-001/SC-002): <c>spectra init --agent</c> — per-agent golden files, the Claude files byte-identical to a
/// plain <c>init</c>, all-agents coexistence, byte-stable re-runs, aliases, auto-detect, dry run, the human-owned host
/// files (R1), user edits (R6), allowlists, retired files and the namespace contract (R8).
/// </summary>
/// <remarks>Golden capture: <c>SPECTRA_GOLDEN_RECORD_104=1</c> rewrites <c>Golden/104/agent-*.txt</c>.</remarks>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class AgentInstallTests : IDisposable
{
    private const string RecordVariable = "SPECTRA_GOLDEN_RECORD_104";
    private const string AllAgents = "claude,copilot,cursor,devin,codex,gemini,antigravity,kiro,opencode";
    private const string AgentsMd = "AGENTS.md";
    private const string GeminiMd = "GEMINI.md";

    private readonly AgentWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    public static TheoryData<string> NonClaudeAgents() =>
        new() { "copilot", "cursor", "devin", "codex", "gemini", "antigravity", "kiro", "opencode" };

    // ---- goldens ----

    [Theory]
    [MemberData(nameof(NonClaudeAgents))]
    public async Task Init_WithOneAgent_MatchesItsGolden(string agent)
    {
        var (exit, _, notices) = await _ws.AgentsAsync(agent);

        Assert.True(exit == ExitCodes.Success, notices);
        Assert.False(Directory.Exists(_ws.PathOf(".claude/skills")), "no Claude skills without --agent claude");
        Assert.False(_ws.Exists(".claude/settings.json"), "no Claude settings without --agent claude");
        AssertGolden($"agent-{agent}.txt", _ws.Snapshot());
    }

    [Fact]
    public async Task Init_AgentClaude_IsByteIdenticalToPlainInit()
    {
        var (exit, _, notices) = await _ws.AgentsAsync("claude");

        Assert.True(exit == ExitCodes.Success, notices);
        Assert.Equal(ReadGolden("init-fresh.txt"), _ws.Snapshot());
    }

    [Fact]
    public async Task Init_AllAgents_MatchesGolden_AndTheClaudeFilesAreUnchanged()
    {
        var (exit, _, notices) = await _ws.AgentsAsync(AllAgents);

        Assert.True(exit == ExitCodes.Success, notices);
        var lines = _ws.SnapshotLines().ToHashSet(StringComparer.Ordinal);
        foreach (var claudeLine in ReadGolden("init-fresh.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.Contains(claudeLine, lines);
        }

        AssertGolden("agent-all.txt", _ws.Snapshot());
    }

    // ---- re-runs ----

    [Fact]
    public async Task Rerun_EveryAgent_WithAndWithoutRegenerate_IsByteStable()
    {
        await _ws.AgentsAsync(AllAgents);
        var installed = _ws.Snapshot();

        var (_, output, _) = await _ws.AgentsAsync(AllAgents);
        Assert.Equal(installed, _ws.Snapshot());
        Assert.DoesNotContain(" 1 written", output, StringComparison.Ordinal);

        foreach (var agent in NonClaudeAgents())
        {
            await _ws.AgentsAsync(agent, regenerate: true);
            Assert.True(installed == _ws.Snapshot(), $"--agent {agent} --regenerate changed the workspace");
        }
    }

    [Fact]
    public async Task ExistingClaudeWorkspace_AddingAnAgent_LeavesConfigAndClaudeFilesAlone()
    {
        Assert.Equal(ExitCodes.Success, await _ws.InitAsync());
        var before = _ws.SnapshotLines();

        var (exit, output, notices) = await _ws.AgentsAsync("claude,copilot,codex");

        Assert.True(exit == ExitCodes.Success, notices);
        Assert.Contains("claude: already initialised", output, StringComparison.Ordinal);
        var after = _ws.SnapshotLines().ToHashSet(StringComparer.Ordinal);
        foreach (var line in before)
        {
            Assert.Contains(line, after);
        }

        Assert.Contains("skills: reuse .claude/skills (Claude's copy) for copilot", output, StringComparison.Ordinal);
        Assert.Contains("copilot subagents: reuse .claude/agents", output, StringComparison.Ordinal);
        Assert.False(_ws.Exists(".github/skills/spectra-generate/SKILL.md"));
        Assert.True(_ws.Exists(".agents/skills/spectra-generate/SKILL.md"));
    }

    [Fact]
    public async Task UserEditedFile_IsSkipped_UnlessRegenerate()
    {
        await _ws.AgentsAsync("kiro");
        const string edited = ".kiro/agents/spectra-critic.md";
        _ws.Write(edited, "my own critic\n");

        var (_, output, _) = await _ws.AgentsAsync("kiro");
        Assert.Equal("my own critic\n", _ws.Read(edited));
        Assert.Contains($"skipped (edited by you): {edited}", output, StringComparison.Ordinal);

        await _ws.AgentsAsync("kiro", regenerate: true);
        Assert.StartsWith("---\nname: spectra-critic\n", _ws.Read(edited), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileAnAgentNoLongerPlans_IsRetired_OnlyWhenUnedited()
    {
        await _ws.AgentsAsync("devin");
        AddRecordedFile("devin", ".devin/skills/spectra-old/SKILL.md", "old skill\n");
        AddRecordedFile("devin", ".devin/skills/spectra-older/SKILL.md", "older skill\n");
        _ws.Write(".devin/skills/spectra-older/SKILL.md", "edited by the user\n");

        var (_, output, _) = await _ws.AgentsAsync("devin");

        Assert.False(_ws.Exists(".devin/skills/spectra-old/SKILL.md"));
        Assert.False(Directory.Exists(_ws.PathOf(".devin/skills/spectra-old")), "the empty skill folder is removed too");
        Assert.Equal("edited by the user\n", _ws.Read(".devin/skills/spectra-older/SKILL.md"));
        Assert.Contains("1 removed", output, StringComparison.Ordinal);
    }

    // ---- --agent parsing ----

    [Fact]
    public async Task Windsurf_IsDevin_WithANotice_AndAgy_IsAntigravity()
    {
        var (exit, output, notices) = await _ws.AgentsAsync("windsurf,agy");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("'windsurf' is now Devin Desktop", notices, StringComparison.Ordinal);
        Assert.Contains("devin (Devin)", output, StringComparison.Ordinal);
        Assert.Contains("antigravity (Antigravity CLI)", output, StringComparison.Ordinal);
        Assert.True(_ws.Exists(".devin/rules/spectra.md"));
        Assert.True(_ws.Exists(".agents/rules/spectra.md"));
    }

    [Theory]
    [InlineData("vim", "Unknown --agent 'vim'")]
    [InlineData("auto,claude", "cannot be combined")]
    [InlineData(",", "needs at least one")]
    public async Task BadAgentValue_IsAUsageError_AndWritesNothing(string value, string message)
    {
        var (exit, _, notices) = await _ws.AgentsAsync(value);

        Assert.Equal(ExitCodes.ValidationError, exit);
        Assert.Contains(message, notices, StringComparison.Ordinal);
        Assert.Empty(_ws.Files());
    }

    [Fact]
    public async Task Auto_NothingDetected_IsAUsageError()
    {
        var (exit, _, notices) = await _ws.AgentsAsync("auto");

        Assert.Equal(ExitCodes.ValidationError, exit);
        Assert.Contains("No AI agent detected", notices, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auto_OneAgentsMarkers_InstallsThatAgent()
    {
        Directory.CreateDirectory(_ws.PathOf(".kiro"));

        var (exit, output, notices) = await _ws.AgentsAsync("auto");

        Assert.True(exit == ExitCodes.Success, notices);
        Assert.Contains("kiro (Kiro CLI)", output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_ws.PathOf(".claude/skills")));
    }

    [Fact]
    public async Task Auto_SeveralAgentsMarkers_ListsThem_AndGuessesNothing()
    {
        Directory.CreateDirectory(_ws.PathOf(".kiro"));
        Directory.CreateDirectory(_ws.PathOf(".cursor"));

        var (exit, _, notices) = await _ws.AgentsAsync("auto");

        Assert.Equal(ExitCodes.ValidationError, exit);
        Assert.Contains("Several AI agents detected (cursor, kiro)", notices, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auto_AfterAnInstall_RefreshesTheInstalledAgents()
    {
        await _ws.AgentsAsync("claude,gemini");
        Directory.CreateDirectory(_ws.PathOf(".kiro")); // a marker alone does not add an agent once some are installed

        var (_, output, _) = await _ws.AgentsAsync("auto");

        Assert.Contains("gemini (Gemini CLI)", output, StringComparison.Ordinal);
        Assert.Contains("claude: already initialised", output, StringComparison.Ordinal);
        Assert.DoesNotContain("kiro", output, StringComparison.Ordinal);
    }

    // ---- dry run ----

    [Fact]
    public async Task DryRun_WritesNothing_AndListsThePlan()
    {
        var (exit, output, _) = await _ws.AgentsAsync("claude,cursor,codex", dryRun: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Empty(_ws.Files());
        Assert.Contains("dry run — nothing is written", output, StringComparison.Ordinal);
        Assert.Contains("claude: would install", output, StringComparison.Ordinal);
        Assert.Contains("write    .cursor/rules/spectra.mdc", output, StringComparison.Ordinal);
        Assert.Contains("write    .agents/skills/spectra-generate/SKILL.md", output, StringComparison.Ordinal);
        Assert.Contains("write    .codex/agents/spectra-critic.toml", output, StringComparison.Ordinal);
        Assert.Contains("codex allowlist: none (no documented project-level command allowlist) — skipped", output, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md: SPECTRA block would be created", output, StringComparison.Ordinal);
        Assert.Contains(".cursor/cli.json permissions.allow += \"Shell(spectra)\" (would be added)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkipSkills_InstallsNoAgentFiles()
    {
        var (exit, output, _) = await _ws.AgentsAsync("cursor", skipSkills: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("--skip-skills: no agent files installed", output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_ws.PathOf(".cursor")));
    }

    // ---- R1: human-owned host files ----

    [Fact]
    public async Task AgentsMd_TheHumansBytesAreKept_AndOneBlockIsAdded()
    {
        const string human = "# Team rules\r\n\r\nUse tabs.";
        _ws.Write(AgentsMd, human);

        await _ws.AgentsAsync("codex,opencode");
        var once = _ws.Read(AgentsMd);
        await _ws.AgentsAsync("opencode");
        await _ws.AgentsAsync("codex", regenerate: true);

        Assert.StartsWith(human, once, StringComparison.Ordinal);
        Assert.Equal(once, _ws.Read(AgentsMd));
        Assert.Equal(1, Count(once, MarkerBlock.Open));
        Assert.Equal(1, Count(once, MarkerBlock.Close));
    }

    [Fact]
    public async Task GeminiMd_AChangedBlockIsReplacedInPlace_AndNothingElseMoves()
    {
        var human = $"# Mine\n\n{MarkerBlock.Open}\n@./old/path.md\n{MarkerBlock.Close}\n\nMore of mine.\n";
        _ws.Write(GeminiMd, human);

        await _ws.AgentsAsync("gemini");

        Assert.Equal($"# Mine\n\n{MarkerBlock.Open}\n@./.spectra/spectra.instructions.md\n{MarkerBlock.Close}\n\nMore of mine.\n",
            _ws.Read(GeminiMd));
    }

    // ---- allowlists ----

    [Fact]
    public async Task Allowlists_AreMergedByKey_AndNeverOverwriteTheUsersValues()
    {
        _ws.Write(".cursor/cli.json", "{\n  \"permissions\": { \"allow\": [\"Shell(git)\"], \"deny\": [\"Shell(rm)\"] },\n  \"editor\": { \"vimMode\": true }\n}\n");
        _ws.Write("opencode.json", "{\n  \"permission\": { \"bash\": { \"*\": \"ask\", \"spectra *\": \"deny\" } },\n  \"theme\": \"dark\"\n}\n");

        await _ws.AgentsAsync("cursor,opencode");

        var cursor = JsonNode.Parse(_ws.Read(".cursor/cli.json"))!;
        Assert.Equal(["Shell(git)", "Shell(spectra)"], cursor["permissions"]!["allow"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("Shell(rm)", cursor["permissions"]!["deny"]![0]!.GetValue<string>());
        Assert.True(cursor["editor"]!["vimMode"]!.GetValue<bool>());

        var opencode = JsonNode.Parse(_ws.Read("opencode.json"))!;
        Assert.Equal("deny", opencode["permission"]!["bash"]!["spectra *"]!.GetValue<string>());
        Assert.Equal("dark", opencode["theme"]!.GetValue<string>());
    }

    [Fact]
    public async Task Allowlist_InAFileWithComments_IsSkippedWithAReason_AndTheFileIsUntouched()
    {
        const string jsonc = "{\n  // my settings\n  \"permissions\": { \"allow\": [] }\n}\n";
        _ws.Write(".devin/config.json", jsonc);

        var (_, output, _) = await _ws.AgentsAsync("devin");

        Assert.Equal(jsonc, _ws.Read(".devin/config.json"));
        Assert.Contains("allowlist: skipped — .devin/config.json is not plain JSON", output, StringComparison.Ordinal);
    }

    // ---- R8: namespace ----

    [Fact]
    public async Task EveryFileTheAgentInstallOwns_IsInSpectrasNamespace()
    {
        await _ws.AgentsAsync(AllAgents);

        foreach (var agent in NonClaudeAgents())
        {
            var manifest = AgentManifest.Load(_ws.Root, agent)!;
            Assert.NotEmpty(manifest.Files);
            foreach (var path in manifest.Files.Select(f => f.Path))
            {
                Assert.True(
                    path.Contains("/spectra-", StringComparison.Ordinal) || path.Contains("/spectra.", StringComparison.Ordinal)
                    || path.StartsWith(".spectra/", StringComparison.Ordinal),
                    $"{agent} owns '{path}', outside SPECTRA's namespace");
                Assert.True(_ws.Exists(path), $"{agent}'s manifest lists missing '{path}'");
            }
        }

        Assert.False(_ws.Exists("CLAUDE.md"), "SPECTRA never writes CLAUDE.md");
    }

    // ---- helpers ----

    private void AddRecordedFile(string agent, string relative, string content)
    {
        _ws.Write(relative, content);
        var manifest = AgentManifest.Load(_ws.Root, agent)!;
        manifest.Files.Add(new AgentManifestEntry { Path = relative, Sha256 = AgentManifest.Hash(content) });
        manifest.Save(_ws.Root);
    }

    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static void AssertGolden(string name, string actual)
    {
        var path = Path.Combine(GoldenDirectory(), name);
        if (Environment.GetEnvironmentVariable(RecordVariable) == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"golden file missing: {path} (capture with {RecordVariable}=1)");
        Assert.Equal(ReadGolden(name), actual);
    }

    private static string ReadGolden(string name) => File.ReadAllText(Path.Combine(GoldenDirectory(), name)).Replace("\r\n", "\n");

    internal static string GoldenDirectory() => Path.Combine(RepoRoot(), "tests", "Spectra.CLI.Tests", "Golden", "104");

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("not inside the repository");
    }
}
