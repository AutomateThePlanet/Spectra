using System.Text.Json.Nodes;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Config;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 097 F7 (FR-020–FR-023; SC-006; analyze U1, U2, A2): <c>ai.critic.model</c> (default <c>sonnet</c>) selects the
/// critic's model. <c>init</c> writes it into the config and the installed agent; <c>update-skills</c> applies a
/// changed value to an unedited agent and never to an edited one. <c>update-skills</c> never fails on the config: an
/// invalid one (any reason) gives one warning, keeps the critic's installed model line, refreshes the rest, exit 0. On
/// 807047c the agent pinned <c>claude-sonnet-4-6</c> and nothing read the key.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CriticModelTests : IDisposable
{
    private const string InvalidWarning = "the critic agent's model was not changed";
    private const string SomeSkill = ".claude/skills/spectra-help/SKILL.md";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public CriticModelTests() => _ws.InitWithSkillsAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private async Task<string> UpdateSkillsAsync()
    {
        var (exit, report) = await _ws.UpdateSkillsReportAsync();
        Assert.True(exit == ExitCodes.Success, report);
        return report;
    }

    private string ConfigModel() =>
        JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!["ai"]?["critic"]?["model"]?.GetValue<string>() ?? "(none)";

    [Fact]
    public void TheRealInit_WritesTheDefaultIntoTheConfigAndTheAgent()
    {
        Assert.Equal("sonnet", ConfigModel());
        Assert.Equal("sonnet", _ws.CriticModel());
    }

    [Theory]
    [InlineData("opus")]
    [InlineData("inherit")]
    [InlineData("claude-opus-4-6[1m]")]
    public async Task AChangedKey_IsAppliedToAnUneditedAgent(string model)
    {
        _ws.SetCriticModel(model);

        var report = await UpdateSkillsAsync();

        Assert.Equal(model, _ws.CriticModel());
        Assert.Contains("spectra-critic.agent.md", report[report.IndexOf("Updated:", StringComparison.Ordinal)..]);
    }

    [Fact]
    public async Task AnEditedAgent_IsNeverOverwritten()
    {
        _ws.Append(VerificationWorkspace.CriticAgent, "\nTeam note.\n");
        var before = File.ReadAllBytes(_ws.PathOf(VerificationWorkspace.CriticAgent));
        _ws.SetCriticModel("haiku");

        var report = await UpdateSkillsAsync();

        Assert.Equal(before, File.ReadAllBytes(_ws.PathOf(VerificationWorkspace.CriticAgent)));
        Assert.Contains("spectra-critic.agent.md", report[report.IndexOf("Skipped (customized by user)", StringComparison.Ordinal)..]);
    }

    [Fact]
    public async Task APre097PinnedAgent_MovesToTheDefault()
    {
        _ws.PinnedCriticAgent();
        _ws.EditConfig(c => (c["ai"] as JsonObject)?.Remove("critic"));

        await UpdateSkillsAsync();

        Assert.Equal("sonnet", _ws.CriticModel());
    }

    // Spec 098 F1 (deliberate change): an invalid ai.critic.model no longer makes the config invalid — it is ignored with
    // one warning and the default is installed (was: CONFIG_INVALID warning, installed model line kept).
    [Theory]
    [InlineData("two words")]
    [InlineData("")]
    [InlineData("a\"b")]
    [InlineData("[1m]")]
    public async Task AnInvalidModel_OnlyWarns_AndInstallsTheDefault(string model)
    {
        _ws.PinnedCriticAgent();
        File.Delete(_ws.PathOf(SomeSkill));
        _ws.SetCriticModel(model);

        var report = await UpdateSkillsAsync();

        Assert.DoesNotContain("CONFIG_INVALID", report);
        Assert.DoesNotContain(InvalidWarning, report);
        Assert.Equal(1, CountOf(report, "using sonnet"));
        Assert.Equal("sonnet", _ws.CriticModel());
        Assert.True(File.Exists(_ws.PathOf(SomeSkill)), "the other skills are refreshed as usual");
    }

    [Fact]
    public async Task ANonStringModel_OnlyWarns_NamingTheKey()
    {
        _ws.PinnedCriticAgent();
        _ws.SetCriticModel(JsonValue.Create(5));

        var report = await UpdateSkillsAsync();

        Assert.DoesNotContain("CONFIG_INVALID", report);
        Assert.Contains("ai.critic.model 5 is not valid", report);
        Assert.Equal("sonnet", _ws.CriticModel());
    }

    [Theory]
    [InlineData("range")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    public async Task AConfigInvalidForAnotherReason_OnlyWarns(string kind)
    {
        _ws.PinnedCriticAgent();
        File.Delete(_ws.PathOf(SomeSkill));
        var path = _ws.PathOf("spectra.config.json");
        switch (kind)
        {
            case "range":
                _ws.EditConfig(c => c["update"] = new JsonObject { ["group_size"] = 0 });
                break;
            case "duplicate":
                File.WriteAllText(path, File.ReadAllText(path).TrimEnd().TrimEnd('}') + ",\n  \"ai\": {}\n}\n");
                break;
            default:
                File.WriteAllText(path, "{ not json");
                break;
        }

        var report = await UpdateSkillsAsync();

        Assert.Contains("spectra.config.json is invalid (", report);
        Assert.Contains(InvalidWarning, report);
        Assert.Equal(VerificationWorkspace.PinnedModel, _ws.CriticModel());
        Assert.True(File.Exists(_ws.PathOf(SomeSkill)));
    }

    // Spec 098 F1 (deliberate change): no command fails because of ai.critic (was: CONFIG_INVALID).
    [Fact]
    public async Task CommandsThatNeedAValidConfig_AcceptAModelStartingWithABracket_AndUseTheDefault()
    {
        _ws.SetCriticModel("[1m]");

        var loaded = await ConfigLoader.LoadForWriteAsync(_ws.Root);

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Equal("sonnet", loaded.Config!.Ai.EffectiveCriticModel);
    }

    // Spec 098 F1 (deliberate change): a retired key makes the block legacy, so the model next to it is ignored (was:
    // honoured) — the v1.45 template wrote provider + a GitHub Models model.
    [Fact]
    public async Task TheRetiredKeys_StillGetTheNotice_AndTheModelNextToThemIsIgnored()
    {
        _ws.EditConfig(c => c["ai"]!["critic"] = new JsonObject { ["provider"] = "anthropic", ["model"] = "opus" });

        Assert.Contains("ai.critic.provider", ConfigLoader.DetectDeprecatedKeys(File.ReadAllText(_ws.PathOf("spectra.config.json"))));
        var report = await UpdateSkillsAsync();
        Assert.Equal("sonnet", _ws.CriticModel());
        Assert.Contains("from the old in-process critic is ignored", report);
    }

    [Theory]
    [InlineData("no ai")]
    [InlineData("critic null")]
    public async Task NoCriticSetting_MeansTheDefault(string kind)
    {
        _ws.PinnedCriticAgent();
        _ws.EditConfig(c =>
        {
            if (kind == "no ai")
            {
                c.Remove("ai");
            }
            else
            {
                c["ai"]!["critic"] = null;
            }
        });

        var report = await UpdateSkillsAsync();

        Assert.DoesNotContain(InvalidWarning, report);
        Assert.Equal("sonnet", _ws.CriticModel());
    }

    [Fact]
    public void TheDeadInProcessCriticCode_IsGone()
    {
        var types = typeof(Program).Assembly.GetTypes().Select(t => t.Name).ToHashSet();

        Assert.DoesNotContain("CriticFactory", types);
        Assert.DoesNotContain("CriticModelResolver", types);
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
