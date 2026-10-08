using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.UpdatePromptWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 088 T1/T2 at the command level (FR-001–FR-006): <c>compile-update-prompt --test-id</c> shows the test's doc
/// sections (current, previous, what changed) and only its linked criteria, and refuses only when there is nothing to
/// reconcile against.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CompileUpdatePromptContextTests : IDisposable
{
    private const string Storage =
        "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space. Users must log in first.\n\n"
        + "## Sharing\n\nFiles can be shared by link.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public CompileUpdatePromptContextTests()
    {
        _ws.WriteDoc("docs/storage.md", Storage);
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 30GB"), ("AC-2", "Files MAY be shared by link"));
    }

    public void Dispose() => _ws.Dispose();

    private void Edit(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    [Fact]
    public async Task TestId_ShowsTheChangedSection_TheUnchangedOne_AndOnlyTheLinkedCriterion()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas", "docs/storage.md#Sharing"], ["AC-1"]);
        await _ws.InitBaselineAsync();
        Edit("30GB", "45GB");

        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.True(exit == 0, error);
        Assert.Contains("#### S1 — docs/storage.md#Quotas  (tests: TC-101)", prompt);
        Assert.Contains("State: changed (LOAD) — numbers: 30gb→45gb", prompt);
        Assert.Contains("Current section:", prompt);
        Assert.Contains("**45GB**", prompt);
        Assert.Contains("Previous version (normalised: lowercased, markup removed):", prompt);
        Assert.Contains("each user gets 30gb", prompt);
        Assert.Contains("What changed: removed \"30gb\"; added \"45gb\"", prompt);
        Assert.Contains("#### S2 — docs/storage.md#Sharing  (tests: TC-101)\nState: unchanged", prompt.Replace("\r\n", "\n"));
        Assert.Contains("- AC-1: Each user MUST get 30GB", prompt);
        Assert.DoesNotContain("AC-2", prompt);
        Assert.Equal(1, Count(prompt, "### TEST TC-101"));
    }

    [Fact]
    public async Task TheLightLoadLabel_MatchesTriage_WithACustomThreshold()
    {
        // Analysis C1: the prompt classifies with update.light_threshold_words, exactly as triage does.
        var config = JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!;
        config["update"] = new JsonObject { ["light_threshold_words"] = 2 };
        File.WriteAllText(_ws.PathOf("spectra.config.json"), config.ToJsonString());
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Sharing"]);
        await _ws.InitBaselineAsync();
        Edit("Files can be shared by link.", "Folders and files are shareable through public links.");

        var triage = await _ws.TriageAsync("storage", "--dry-run");
        var bucket = BaselineTestWorkspace.GroupOf(triage, "TC-101").GetProperty("bucket").GetString();
        var (_, prompt, _) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.Equal("LOAD", bucket); // more than 2 words changed
        Assert.Contains($"State: changed ({bucket})", prompt);
    }

    [Fact]
    public async Task NothingToReconcile_IsRefused_WithTheReason()
    {
        _ws.WriteDoc("docs/gone.md", "# Gone\n\n## Quotas\n\nOld.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/gone.md#Quotas"], ["AC-9"]);
        File.Delete(_ws.PathOf("docs/gone.md"));

        var (exit, output, error) = await _ws.CompileJsonAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.Equal(4, exit);
        Assert.Empty(output);
        var failure = Failure(error);
        Assert.Equal("compile-update-prompt", failure.GetProperty("command").GetString());
        Assert.False(failure.GetProperty("success").GetBoolean());
        Assert.Equal("NOTHING_TO_RECONCILE", failure.GetProperty("error_code").GetString());
        Assert.Contains("TC-101", failure.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task OnlyALinkedCriterion_StillGivesAPrompt()
    {
        _ws.WriteDoc("docs/gone.md", "# Gone\n\n## Quotas\n\nOld.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/gone.md#Quotas"], ["AC-1"]);
        File.Delete(_ws.PathOf("docs/gone.md"));

        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.True(exit == 0, error);
        Assert.Contains("State: REMOVED — not found; not in a git repository", prompt);
        // Spec 089 (F4): no baseline here, so the criterion is labelled not baselined (was "- AC-1: …" on 088).
        Assert.Contains("- AC-1 (not baselined — compare the test with the current text): Each user MUST get 30GB", prompt);
    }

    [Theory]
    [InlineData("TEST_NOT_FOUND", "--suite", "storage", "--test-id", "TC-999")]
    [InlineData("SUITE_NOT_FOUND", "--suite", "nope", "--test-id", "TC-101")]
    [InlineData("INVALID_ARGUMENTS", "--suite", "storage")]
    [InlineData("INVALID_ARGUMENTS", "--suite", "storage", "--test-id", "TC-101", "--group", "G1")]
    [InlineData("INVALID_ARGUMENTS", "--test-id", "TC-101")]
    public async Task Refusals_AreOneJsonDocument_Exit4(string code, params string[] args)
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);

        var (exit, output, error) = await _ws.CompileJsonAsync(args);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Equal(code, Failure(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task NoConfig_RunsOnDefaults()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        File.Delete(_ws.PathOf("spectra.config.json"));

        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.True(exit == 0, error);
        Assert.Contains("### TEST TC-101", prompt);
    }
}
