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
        Assert.Equal(Path.Combine(_dir, ".claude", "skills", "spectra-generation", "SKILL.md"),
            SkillInstallLayout.AgentPath(_dir, "spectra-generation.agent.md"));
        Assert.Equal(Path.Combine(_dir, ".claude", "agents", "spectra-critic.agent.md"),
            SkillInstallLayout.AgentPath(_dir, "spectra-critic.agent.md"));
        // Spec 057: the execution agent is now a main-session skill under .claude/skills/.
        Assert.Equal(Path.Combine(_dir, ".claude", "skills", "spectra-execution", "SKILL.md"),
            SkillInstallLayout.AgentPath(_dir, "spectra-execution.agent.md"));
    }

    // ---------- end-to-end install ----------

    [Fact]
    public async Task Install_WritesAuthoringSkills_UnderClaude()
    {
        var exit = await new InitHandler(_logger, _dir).HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);

        // A representative authoring skill + the generation skill land under .claude/skills/.
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-coverage", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-generation", "SKILL.md")));
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
    public async Task Install_PlacesExecutionAgent_UnderClaudeSkills()
    {
        var exit = await new InitHandler(_logger, _dir).HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exit);

        // Spec 057: the execution agent is ported to a .claude/skills/ main-session skill...
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "skills", "spectra-execution", "SKILL.md")));
        // ...and the legacy .github/ install (agent + skill) is retired.
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
