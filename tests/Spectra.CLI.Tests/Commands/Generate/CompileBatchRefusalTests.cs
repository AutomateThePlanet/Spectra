using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 094 T5 (FR-010, FR-010a, FR-011): <c>compile-prompt --batch</c> refuses (exit 4, the shared failure shape) when
/// the plan is missing or unreadable, the batch is unknown, a section changed since the analysis, no criteria apply, or
/// the prompt is too large — in that order — and never refuses over a pre-094 <c>test-generation.md</c>: an unedited copy
/// compiles with the bundled template, a customised (or unrecorded) one compiles that batch in count mode, each with a
/// warning. On 55b541f there was no <c>--batch</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CompileBatchRefusalTests : IDisposable
{
    /// <summary>The test-generation template as 093 shipped it (no batch placeholders) — the start is enough to predate 094.</summary>
    private const string PreBatchTemplate =
        "---\nspectra_version: \"1.0\"\ntemplate_id: test-generation\ndescription: \"How SPECTRA generates test cases from analyzed behaviors\"\n"
        + "placeholders:\n  - name: behaviors\n    description: \"Analyzed behaviors from phase 1\"\n  - name: count\n    description: \"Number of tests to generate\"\n"
        + "  - name: acceptance_criteria\n    description: \"Related acceptance criteria\"\n---\n\n"
        + "You are a test case generation expert.\n\nGenerate {{count}} new manual test cases based on this request:\n\n{{behaviors}}\n\n"
        + "{{#if acceptance_criteria}}\n## ACCEPTANCE CRITERIA — MANDATORY\n\n{{acceptance_criteria}}\n{{/if}}\n";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public CompileBatchRefusalTests() => SetUpAsync(_ws).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private Task PlanAsync() => _ws.PlanAsync(AnalysisJson(
        ("Quota refused", "docs/storage.md#upload-quota"),
        ("Links expire", "docs/sharing.md#links"),
        ("Audit log kept", "docs/nope.md")));

    private static JsonElement Failure(string stderr, string code, int exit)
    {
        Assert.Equal(4, exit);
        var failure = CompileLine(stderr);
        Assert.False(failure.GetProperty("success").GetBoolean());
        Assert.Equal(code, failure.GetProperty("error_code").GetString());
        return failure;
    }

    private void Edit(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    private string TemplatePath => _ws.PathOf(".spectra/prompts/test-generation.md");

    /// <summary>Overwrites the project's template copy and records its hash in the skills manifest under <paramref name="key"/>.</summary>
    private void InstallTemplateCopy(string content, string? key)
    {
        File.WriteAllText(TemplatePath, content);
        var manifestPath = _ws.PathOf(".spectra/skills-manifest.json");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var files = manifest["files"]!.AsObject();
        foreach (var existing in files.Select(f => f.Key).Where(k => k.Replace('\\', '/').EndsWith(".spectra/prompts/test-generation.md", StringComparison.Ordinal)).ToList())
        {
            files.Remove(existing);
        }

        if (key is not null)
        {
            files[key] = Spectra.CLI.Infrastructure.FileHasher.ComputeHash(content);
        }

        File.WriteAllText(manifestPath, manifest.ToJsonString());
    }

    // ---- refusals ----

    [Fact]
    public async Task NoPlan_IsPlanMissing()
    {
        var (exit, output, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        Failure(error, "PLAN_MISSING", exit);
        Assert.Equal("", output);
    }

    [Fact]
    public async Task AnUnreadablePlan_IsPlanMissing_AndSaysSo()
    {
        Directory.CreateDirectory(_ws.PathOf(".spectra/generation-plan"));
        File.WriteAllText(_ws.PathOf(".spectra/generation-plan/storage.json"), "{not json");

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        var failure = Failure(error, "PLAN_MISSING", exit);
        Assert.Contains("unreadable", failure.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task AnUnknownBatch_IsBatchNotFound_ListingTheIds()
    {
        await PlanAsync();

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "99");

        var message = Failure(error, "BATCH_NOT_FOUND", exit).GetProperty("errors")[0].GetString()!;
        Assert.Contains("1, 2, unsourced", message);
    }

    [Fact]
    public async Task AnEditedSection_IsPlanStale_AndOtherBatchesStillCompile()
    {
        await PlanAsync();
        Edit("Each user gets **30 GB**.", "Each user gets **45 GB**.");

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");
        var (sharingExit, _, sharingError) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        var errors = GitWorkspace.Strings(Failure(error, "PLAN_STALE", exit).GetProperty("errors"));
        Assert.Contains(errors, e => e.Contains("docs/storage.md#upload-quota changed since the analysis", StringComparison.Ordinal));
        Assert.True(sharingExit == 0, sharingError);
    }

    [Fact]
    public async Task ARenamedHeading_IsPlanStale_NoLongerResolves()
    {
        await PlanAsync();
        Edit("## Upload quota", "## Upload limits");

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        var errors = GitWorkspace.Strings(Failure(error, "PLAN_STALE", exit).GetProperty("errors"));
        Assert.Contains(errors, e => e.Contains("docs/storage.md#upload-quota no longer resolves: anchor not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoCriteriaForTheBatchsDocuments_IsMissingInput()
    {
        await PlanAsync();
        File.Delete(_ws.PathOf("docs/criteria/docs/sharing.criteria.yaml"));

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        var failure = Failure(error, "MISSING_INPUT", exit);
        Assert.Equal("criteria_context", failure.GetProperty("missing_input").GetString());
    }

    [Fact]
    public async Task APromptOverTheBudget_IsPromptTooLarge()
    {
        await PlanAsync();
        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!.AsObject();
        config["analysis"] = new System.Text.Json.Nodes.JsonObject { ["max_prompt_tokens"] = 100 };
        File.WriteAllText(_ws.PathOf("spectra.config.json"), config.ToJsonString());

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Failure(error, "PROMPT_TOO_LARGE", exit);
    }

    [Fact]
    public async Task TheChecks_RunInOrder_StalenessBeforeCriteria()
    {
        await PlanAsync();
        _ws.WriteDoc("docs/sharing.md", SharingDoc.Replace("7 days", "9 days"));
        File.Delete(_ws.PathOf("docs/criteria/docs/sharing.criteria.yaml"));

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        Failure(error, "PLAN_STALE", exit);
    }

    [Fact]
    public async Task TheUnsourcedBatch_OnlyChecksPlanAndBatch_ThenCountMode()
    {
        await PlanAsync();
        Edit("Each user gets **30 GB**.", "Each user gets **45 GB**.");

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "unsourced");

        Assert.True(exit == 0, error);
        Assert.Equal("count", CompileLine(error).GetProperty("mode").GetString());
    }

    [Fact]
    public async Task TheUnsourcedBatch_WithoutCriteria_RefusesInTheSharedShape()
    {
        await PlanAsync();
        File.Delete(_ws.PathOf("docs/criteria/docs/storage.criteria.yaml"));
        File.Delete(_ws.PathOf("docs/criteria/docs/sharing.criteria.yaml"));
        foreach (var other in Directory.GetFiles(_ws.PathOf("docs/criteria"), "*.criteria.yaml", SearchOption.AllDirectories))
        {
            File.Delete(other);
        }

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "unsourced");

        var failure = Failure(error, "MISSING_INPUT", exit);
        Assert.Equal("criteria_context", failure.GetProperty("missing_input").GetString());
    }

    // ---- pre-094 template copies: never refused (FR-010a) ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnUneditedPreBatchCopy_CompilesInBatchMode_WithTheBundledTemplate(bool absoluteKey)
    {
        await PlanAsync();
        InstallTemplateCopy(PreBatchTemplate, absoluteKey ? TemplatePath : Path.Combine(".spectra", "prompts", "test-generation.md"));

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        var line = CompileLine(error);
        Assert.Equal("batch", line.GetProperty("mode").GetString());
        Assert.Contains("complete source for this batch", prompt);
        Assert.Contains(GitWorkspace.Strings(line.GetProperty("warnings")),
            w => w == "using the bundled test-generation template; run spectra update-skills to refresh your copy");
    }

    [Fact]
    public async Task ACustomisedPreBatchCopy_CompilesThatBatchInCountMode_WithTheUsersText()
    {
        await PlanAsync();
        InstallTemplateCopy(PreBatchTemplate, Path.Combine(".spectra", "prompts", "test-generation.md"));
        File.AppendAllText(TemplatePath, "\nHOUSE RULE: write steps in the imperative.\n");

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        var line = CompileLine(error);
        Assert.Equal("count_fallback", line.GetProperty("mode").GetString());
        Assert.Contains("HOUSE RULE: write steps in the imperative.", prompt);
        Assert.Contains("Quota refused (docs/storage.md#upload-quota)", prompt);
        Assert.DoesNotContain("Links expire", prompt);
        Assert.Contains("Generate 1 new manual test cases", prompt);
        Assert.Contains(GitWorkspace.Strings(line.GetProperty("warnings")), w => w.StartsWith(
            "custom test-generation template predates batch prompts — this batch is compiled in count mode (the generator reads the docs); run spectra prompts reset test-generation",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task APreBatchCopyWithoutAManifestRecord_CompilesInCountMode_NamingUpdateSkillsToo()
    {
        await PlanAsync();
        InstallTemplateCopy(PreBatchTemplate, key: null);

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        var line = CompileLine(error);
        Assert.Equal("count_fallback", line.GetProperty("mode").GetString());
        Assert.Contains(GitWorkspace.Strings(line.GetProperty("warnings")), w => w.Contains("spectra update-skills if you never edited it", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AfterUpdateSkills_AnUneditedCopy_CompilesInBatchMode_WithoutAWarning()
    {
        await PlanAsync();
        InstallTemplateCopy(PreBatchTemplate, TemplatePath);
        var (updateExit, _, updateError) = await _ws.RunAsync("update-skills");
        Assert.True(updateExit == 0, updateError);

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        var line = CompileLine(error);
        Assert.Equal("batch", line.GetProperty("mode").GetString());
        Assert.DoesNotContain(GitWorkspace.Strings(line.GetProperty("warnings")), w => w.Contains("test-generation template"));
    }
}
