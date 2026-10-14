using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 098 F8 (FR-018, SC-008): <c>compile-prompt --batch</c> decides the template mode before the staleness check. In
/// <c>count_fallback</c> mode (a customised pre-094 template: the generator reads the current docs itself) a changed or
/// unresolvable section is a warning, not <c>PLAN_STALE</c>; batch mode still refuses. On 986da01 any doc edit since the
/// analysis refused the count-mode batch.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CountFallbackStaleTests : IDisposable
{
    private const string PreBatchTemplate =
        "---\nspectra_version: \"1.0\"\ntemplate_id: test-generation\ndescription: \"How SPECTRA generates test cases from analyzed behaviors\"\n"
        + "placeholders:\n  - name: behaviors\n    description: \"Analyzed behaviors from phase 1\"\n  - name: count\n    description: \"Number of tests to generate\"\n"
        + "  - name: acceptance_criteria\n    description: \"Related acceptance criteria\"\n---\n\n"
        + "You are a test case generation expert.\n\nGenerate {{count}} new manual test cases based on this request:\n\n{{behaviors}}\n\n"
        + "{{#if acceptance_criteria}}\n## ACCEPTANCE CRITERIA — MANDATORY\n\n{{acceptance_criteria}}\n{{/if}}\n"
        + "\nHOUSE RULE: write steps in the imperative.\n";

    private const string Warning = "the documentation changed since the analysis; behaviour titles may be outdated";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public CountFallbackStaleTests() => SetUpAsync(_ws).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private Task PlanAsync() => _ws.PlanAsync(AnalysisJson(
        ("Quota refused", "docs/storage.md#upload-quota"),
        ("Links expire", "docs/sharing.md#links")));

    private void Edit(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    /// <summary>A customised pre-094 copy (no manifest record matches it).</summary>
    private void CustomisedTemplate() =>
        File.WriteAllText(_ws.PathOf(".spectra/prompts/test-generation.md"), PreBatchTemplate);

    [Theory]
    [InlineData("Each user gets **30 GB**.", "Each user gets **45 GB**.", "changed since the analysis")]
    [InlineData("## Upload quota", "## Upload limits", "no longer resolves")]
    public async Task CountFallback_WarnsInsteadOfRefusing(string from, string to, string detail)
    {
        await PlanAsync();
        CustomisedTemplate();
        Edit(from, to);

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        var line = CompileLine(error);
        Assert.Equal("count_fallback", line.GetProperty("mode").GetString());
        Assert.Contains("HOUSE RULE: write steps in the imperative.", prompt);
        var warnings = GitWorkspace.Strings(line.GetProperty("warnings"));
        Assert.Contains(warnings, w => w.StartsWith(Warning, StringComparison.Ordinal)
                                       && w.Contains("docs/storage.md#upload-quota", StringComparison.Ordinal)
                                       && w.Contains(detail, StringComparison.Ordinal));
    }

    [Fact]
    public async Task BatchMode_StillRefusesWithPlanStale()
    {
        await PlanAsync();
        Edit("Each user gets **30 GB**.", "Each user gets **45 GB**.");

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.Equal(4, exit);
        Assert.Equal("PLAN_STALE", CompileLine(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task CountFallback_WithoutAChange_HasNoStaleWarning()
    {
        await PlanAsync();
        CustomisedTemplate();

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        Assert.DoesNotContain(GitWorkspace.Strings(CompileLine(error).GetProperty("warnings")), w => w.StartsWith(Warning, StringComparison.Ordinal));
    }
}
