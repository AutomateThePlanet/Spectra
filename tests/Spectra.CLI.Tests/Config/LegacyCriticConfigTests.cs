using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.Commands;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Config;

namespace Spectra.CLI.Tests.Config;

/// <summary>
/// Spec 098 F1 (FR-001–FR-005, SC-001): an <c>ai.critic</c> block of the removed in-process critic (any retired key) is
/// legacy — its <c>model</c> is ignored with a notice and <c>sonnet</c> is used — and an invalid model is only a warning.
/// No command fails because of <c>ai.critic</c>. Rows are built from the real init template with the blocks older
/// templates wrote. On 986da01 <c>update-skills</c> installed <c>gpt-5-mini</c> from the v1.45 block, and
/// <c>openai/gpt-4.1</c> made every command that loads the config fail with CONFIG_INVALID.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class LegacyCriticConfigTests : IDisposable
{
    private const string LegacySentence =
        "ai.critic.model from the old in-process critic is ignored; set it again (alias, model id or inherit) to choose the critic subagent's model";

    /// <summary>The v1.45 init template's block (0e84a69).</summary>
    public static JsonObject V145Block() =>
        new() { ["enabled"] = true, ["provider"] = "github-models", ["model"] = "gpt-5-mini" };

    /// <summary>The 058–068 init template's block (de159cb).</summary>
    public static JsonObject V058Block() =>
        new() { ["enabled"] = true, ["model"] = "claude-sonnet-4-6", ["max_concurrent"] = 1, ["_comment_critic"] = "Spec 058" };

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public LegacyCriticConfigTests()
    {
        _ws.InitWithSkillsAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", VerificationWorkspace.StorageDoc);
        _ws.WriteTest("storage", "TC-101", [VerificationWorkspace.Quotas]);
        _ws.RebuildIndex("storage");
    }

    public void Dispose() => _ws.Dispose();

    private static string TemplateWithCritic(JsonNode? critic)
    {
        var root = JsonNode.Parse(ConfigRuleMatrixTests.InitTemplate(), documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        })!.AsObject();
        root["ai"]!["critic"] = critic;
        return root.ToJsonString();
    }

    private void UseCritic(JsonNode? critic) =>
        File.WriteAllText(_ws.PathOf("spectra.config.json"), TemplateWithCritic(critic));

    public static TheoryData<string, string> TypedRows => new()
    {
        { """{"enabled": true, "provider": "github-models", "model": "gpt-5-mini"}""", "sonnet" },
        { """{"enabled": true, "model": "claude-sonnet-4-6", "max_concurrent": 1}""", "sonnet" },
        { """{"Provider": "anthropic", "Model": "opus"}""", "sonnet" },
        { """{"timeout_seconds": 30, "model": "opus"}""", "sonnet" },
        { """{"api_key_env": "X", "model": "opus"}""", "sonnet" },
        { """{"base_url": "https://x.invalid", "model": "opus"}""", "sonnet" },
        { """{"model": "openai/gpt-4.1"}""", "sonnet" },
        { """{"model": 5}""", "sonnet" },
        { """{"model": ""}""", "sonnet" },
        { """{"model": "[1m]"}""", "sonnet" },
        { """{"model": ["opus"]}""", "sonnet" },
        { "\"opus\"", "sonnet" },
        { "[]", "sonnet" },
        { """{"enabled": true}""", "sonnet" },
        { """{"model": "opus"}""", "opus" },
        { """{"_comment_critic": "x", "model": "inherit"}""", "inherit" },
        { "null", "sonnet" }
    };

    [Theory]
    [MemberData(nameof(TypedRows))]
    public void TheConfigAlwaysLoads_AndTheEffectiveModelFollowsTheRule(string critic, string expected)
    {
        var json = TemplateWithCritic(JsonNode.Parse(critic));

        var result = new ConfigLoader().Load(json);

        Assert.True(result.IsSuccess, result.IsSuccess ? "" : result.Errors[0].Message);
        Assert.Equal(expected, result.Value!.Ai.EffectiveCriticModel);
    }

    [Theory]
    [InlineData("v1.45")]
    [InlineData("v058")]
    [InlineData("openai/gpt-4.1")]
    [InlineData("number")]
    public async Task EveryCommand_RunsAsWithThePlainTemplate(string row)
    {
        string[][] commands =
        [
            ["validate", "--output-format", "json"],
            ["ai", "update-triage", "--all-suites", "--dry-run", "--output-format", "json"],
            ["docs", "changed", "--output-format", "json"],
            ["docs", "list-suites", "--output-format", "json"],
            ["ai", "audit-grounding", "--suite", "none", "--output-format", "json"]
        ];

        var plain = new List<int>();
        foreach (var command in commands)
        {
            plain.Add((await _ws.RunAsync(command)).Exit);
        }

        UseCritic(row switch
        {
            "v1.45" => V145Block(),
            "v058" => V058Block(),
            "number" => new JsonObject { ["model"] = 5 },
            _ => new JsonObject { ["model"] = row }
        });

        for (var i = 0; i < commands.Length; i++)
        {
            var (exit, output, error) = await _ws.RunAsync(commands[i]);
            Assert.True(plain[i] == exit, $"{string.Join(' ', commands[i])}: {plain[i]} → {exit}\n{output}\n{error}");
            Assert.DoesNotContain("CONFIG_INVALID", output + error);
        }
    }

    [Theory]
    [InlineData("v1.45")]
    [InlineData("v058")]
    public async Task UpdateSkills_InstallsSonnet_AndSaysTheLegacyModelIsIgnored(string row)
    {
        UseCritic(row == "v1.45" ? V145Block() : V058Block());

        var (exit, report) = await _ws.UpdateSkillsReportAsync();

        Assert.True(exit == ExitCodes.Success, report);
        Assert.Equal("sonnet", _ws.CriticModel());
        Assert.Equal(1, Count(report, LegacySentence));
        Assert.DoesNotContain("CONFIG_INVALID", report);
    }

    [Theory]
    [InlineData("openai/gpt-4.1")]
    [InlineData("two words")]
    [InlineData("[1m]")]
    public async Task UpdateSkills_InstallsSonnet_ForAnInvalidModel_WithOneWarning(string model)
    {
        _ws.SetCriticModel(model);

        var (exit, report) = await _ws.UpdateSkillsReportAsync();

        Assert.True(exit == ExitCodes.Success, report);
        Assert.Equal("sonnet", _ws.CriticModel());
        Assert.Equal(1, Count(report, "ai.critic.model"));
        Assert.Contains("using sonnet", report);
        Assert.DoesNotContain("CONFIG_INVALID", report);
    }

    [Fact]
    public async Task UpdateSkills_ANewStyleModel_HasNoWarning()
    {
        _ws.SetCriticModel("opus");

        var (exit, report) = await _ws.UpdateSkillsReportAsync();

        Assert.True(exit == ExitCodes.Success, report);
        Assert.Equal("opus", _ws.CriticModel());
        Assert.DoesNotContain("ai.critic", report);
    }

    [Fact]
    public async Task Validate_NamesEveryRetiredKey_AndTheLegacyModelSentence()
    {
        UseCritic(new JsonObject
        {
            ["enabled"] = true, ["provider"] = "github-models", ["model"] = "gpt-5-mini", ["max_concurrent"] = 2, ["timeout_seconds"] = 30
        });

        var (_, output, _) = await _ws.RunAsync("validate", "--output-format", "json");

        var notes = string.Join("\n", BaselineTestWorkspace.Json(output).GetProperty("notes").EnumerateArray().Select(n => n.GetString()));
        foreach (var key in new[] { "ai.critic.enabled", "ai.critic.provider", "ai.critic.max_concurrent", "ai.critic.timeout_seconds" })
        {
            Assert.Contains(key, notes);
        }

        Assert.Contains(LegacySentence, notes);
        Assert.DoesNotContain("is the only critic selector", notes);
    }

    [Fact]
    public async Task Validate_ANewStyleBlock_HasNoNotice()
    {
        var (_, output, _) = await _ws.RunAsync("validate", "--output-format", "json");

        var result = BaselineTestWorkspace.Json(output);
        Assert.False(result.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array && notes.GetArrayLength() > 0);
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
