using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 056 (FR-001 / SC-001 / SC-005) — the authoring orchestration installs as Claude Code skills
/// under <c>.claude/skills/</c> (the generation agent becomes a main-session skill, the critic a
/// <c>.claude/agents/</c> subagent), through the unchanged manifest+hash pipeline. The execution
/// agent keeps its <c>.github/</c> install (excluded — next spec).
/// </summary>
[Collection("WorkingDirectory")]
public sealed class ClaudeSkillsInstallTests : IDisposable
{
    private readonly string _dir;
    private readonly ILogger<InitHandler> _logger;

    public ClaudeSkillsInstallTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-claude-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    // ---------- pure layout contract ----------

    [Fact]
    public void Layout_RoutesArtifactsByRole()
    {
        Assert.Equal(Path.Combine(_dir, ".claude", "skills", "spectra-coverage", "SKILL.md"),
            SkillInstallLayout.SkillPath(_dir, "spectra-coverage"));
        // skill-pair-merge: generation + execution agents merged into flow skills; flow skills
        // install via SkillPath to .claude/skills/<name>/SKILL.md.
        Assert.Equal(Path.Combine(_dir, ".claude", "skills", "spectra-generate", "SKILL.md"),
            SkillInstallLayout.SkillPath(_dir, "spectra-generate"));
        Assert.Equal(Path.Combine(_dir, ".claude", "skills", "spectra-execute", "SKILL.md"),
            SkillInstallLayout.SkillPath(_dir, "spectra-execute"));
        // The critic subagent still routes to .claude/agents/.
        Assert.Equal(Path.Combine(_dir, ".claude", "agents", "spectra-critic.agent.md"),
            SkillInstallLayout.AgentPath(_dir, "spectra-critic.agent.md"));
    }

    // ---------- end-to-end install ----------

    [Fact]
    public async Task Install_WritesAuthoringSkills_UnderClaude()
    {
        var exit = await new InitHandler(_logger, _dir).HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);

        // A representative authoring skill + the merged generate skill land under .claude/skills/.
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-coverage", "SKILL.md")));
        // skill-pair-merge: merged generate/execute flow skills (not agent names) land here.
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-generate", "SKILL.md")));
        // The critic subagent lands under .claude/agents/.
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "agents", "spectra-critic.agent.md")));
    }

    [Fact]
    public async Task Install_PutsZeroAuthoringSkills_UnderGithub()
    {
        var exit = await new InitHandler(_logger, _dir).HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);

        // No bundled authoring skill is installed under .github/skills/.
        foreach (var name in SkillContent.All.Keys)
            Assert.False(File.Exists(Path.Combine(_dir, ".github", "skills", name, "SKILL.md")),
                $"Authoring skill '{name}' must not land under .github/skills/.");
    }

    [Fact]
    public async Task Install_PlacesExecuteSkill_UnderClaudeSkills()
    {
        var exit = await new InitHandler(_logger, _dir).HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);

        // skill-pair-merge: the merged spectra-execute skill (not the old agent) installs here.
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-execute", "SKILL.md")));
        // The old agent-named paths must not exist.
        Assert.False(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-execution", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(_dir, ".github", "agents", "spectra-execution.agent.md")));
        Assert.False(File.Exists(Path.Combine(_dir, ".github", "skills", "spectra-execution", "SKILL.md")));
    }

    [Fact]
    public async Task Install_TracksClaudePaths_InManifest()
    {
        var exit = await new InitHandler(_logger, _dir).HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);

        var manifest = await File.ReadAllTextAsync(Path.Combine(_dir, ".spectra", "skills-manifest.json"));
        // (Path separators are JSON-escaped in the manifest; assert on slash-free substrings.)
        Assert.Contains(".claude", manifest);
        Assert.Contains("spectra-critic.agent.md", manifest);
        Assert.Contains("spectra-coverage", manifest);
    }
}
