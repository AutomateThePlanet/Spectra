using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Skills;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 092 T1–T2 (FR-016, FR-017): every bundled agent installs to <c>.claude/agents/</c> by one rule — the critic and
/// the three orchestration agents (updater, repairer, extractor) — and <c>spectra update-skills</c> adds missing agents to
/// a pre-092 workspace without touching a user-modified file. On 309174b the three agents didn't exist and any agent
/// other than the critic routed to <c>.github/agents/</c>. Workspaces from the real <c>spectra init</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class AgentInstallLayoutTests : IDisposable
{
    private static readonly string[] NewAgents =
        ["spectra-updater.agent.md", "spectra-repairer.agent.md", "spectra-extractor.agent.md"];

    // Spec 093: the generation skill's analyst and generator.
    private static readonly string[] GenerateAgents = ["spectra-analyst.agent.md", "spectra-generator.agent.md"];

    private static readonly string[] AllAgents = ["spectra-critic.agent.md", .. NewAgents, .. GenerateAgents];

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    private string AgentFile(string name) => _ws.PathOf($".claude/agents/{name}");

    private async Task InitWithSkillsAsync()
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        Assert.Equal(ExitCodes.Success, await new InitHandler(logger, _ws.Root).HandleAsync(force: false, skipSkills: false));
    }

    private void AssertNoGithubAgents() =>
        Assert.False(
            Directory.Exists(_ws.PathOf(".github/agents")) && Directory.EnumerateFiles(_ws.PathOf(".github/agents")).Any(),
            "an agent file was installed under .github/agents/");

    // ---- T1 ----

    [Fact]
    public async Task TheRealInit_InstallsEveryBundledAgent_UnderClaudeAgents()
    {
        await InitWithSkillsAsync();

        Assert.All(AllAgents, name => Assert.True(File.Exists(AgentFile(name)), name));
        AssertNoGithubAgents();
    }

    // ---- T1b: one general rule ----

    [Fact]
    public void AgentPath_IsClaudeAgents_ForEveryAgent_IncludingOnesNotYetBundled()
    {
        foreach (var name in AgentContent.All.Keys.Append("spectra-future.agent.md"))
        {
            Assert.Equal(Path.Combine(_ws.Root, ".claude", "agents", name), SkillInstallLayout.AgentPath(_ws.Root, name));
        }

        Assert.All(NewAgents, name => Assert.True(AgentContent.All.ContainsKey(name), name));
    }

    // ---- T2: a pre-092 workspace ----

    [Fact]
    public async Task UpdateSkills_OnAPre092Workspace_AddsTheNewAgents_AndSkipsAModifiedSkill()
    {
        await InitWithSkillsAsync();

        // A workspace from 091: only the critic is installed and recorded.
        var store = new SkillsManifestStore(_ws.Root);
        var manifest = await store.LoadAsync();
        foreach (var name in NewAgents)
        {
            File.Delete(AgentFile(name));
            foreach (var key in manifest.Files.Keys.Where(k => k.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                manifest.Files.Remove(key);
            }
        }

        await store.SaveAsync(manifest);

        // The user customised the update skill.
        var updateSkill = _ws.PathOf(".claude/skills/spectra-update/SKILL.md");
        File.AppendAllText(updateSkill, "\nMy own note.\n");
        var customised = File.ReadAllText(updateSkill);

        var (exit, _, error) = await _ws.RunAsync("update-skills");

        Assert.True(exit == 0, error);
        foreach (var name in NewAgents)
        {
            Assert.True(File.Exists(AgentFile(name)), name);
            Assert.Equal(AgentContent.All[name], File.ReadAllText(AgentFile(name)));
        }

        Assert.Equal(customised, File.ReadAllText(updateSkill));
        AssertNoGithubAgents();
    }

    // ---- Spec 093 T2: a pre-093 workspace (092 agents installed, a customised generation skill) ----

    [Fact]
    public async Task UpdateSkills_OnAPre093Workspace_AddsTheAnalystAndGenerator_AndSkipsAModifiedSkill()
    {
        await InitWithSkillsAsync();

        var store = new SkillsManifestStore(_ws.Root);
        var manifest = await store.LoadAsync();
        foreach (var name in GenerateAgents)
        {
            if (File.Exists(AgentFile(name)))
            {
                File.Delete(AgentFile(name));
            }

            foreach (var key in manifest.Files.Keys.Where(k => k.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                manifest.Files.Remove(key);
            }
        }

        await store.SaveAsync(manifest);

        var generateSkill = _ws.PathOf(".claude/skills/spectra-generate/SKILL.md");
        File.AppendAllText(generateSkill, "\nMy own note.\n");
        var customised = File.ReadAllText(generateSkill);

        var (exit, _, error) = await _ws.RunAsync("update-skills");

        Assert.True(exit == 0, error);
        foreach (var name in GenerateAgents)
        {
            Assert.True(File.Exists(AgentFile(name)), name);
            Assert.Equal(AgentContent.All[name], File.ReadAllText(AgentFile(name)));
        }

        Assert.Equal(customised, File.ReadAllText(generateSkill));
        AssertNoGithubAgents();
    }
}
