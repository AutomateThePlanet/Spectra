using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 094 T4 (FR-009, FR-011–FR-013; R6, R8): <c>compile-prompt --batch</c> inlines exactly the batch's behaviours, its
/// sections once (an outer ref absorbs an inner one) and only their criteria, says the sections are the complete source,
/// never asks the model to read docs or call the dead tools, and is byte-identical when rerun. On 55b541f there was no
/// <c>--batch</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CompileBatchPromptTests : IDisposable
{
    private static readonly string[] DeadText =
        ["ON-DEMAND", "ListDocumentationFiles", "ReadTestIndex", "CheckDuplicates", "GetNextTestIds"];

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public CompileBatchPromptTests() => SetUpAsync(_ws).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    /// <summary>Batch 1 = sharing [B3]; batch 2 = storage [B1 upload-quota, B2 quota-settings].</summary>
    private Task SectionsOnlyAsync() => _ws.PlanAsync(AnalysisJson(
        ("Quota refused", "docs/storage.md#upload-quota"),
        ("Quota shown", "docs/storage.md#quota-settings"),
        ("Links expire", "docs/sharing.md#links")));

    [Fact]
    public async Task TheBatchPrompt_HoldsOnlyItsBehavioursSectionsAndCriteria()
    {
        await SectionsOnlyAsync();

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        Assert.Contains("- B1 [happy_path · EP] Quota refused (docs/storage.md#upload-quota)", prompt);
        Assert.Contains("- B2 [happy_path · EP] Quota shown (docs/storage.md#quota-settings)", prompt);
        Assert.DoesNotContain("Links expire", prompt);
        Assert.DoesNotContain("A link expires after 7 days", prompt);

        // The nested section is absorbed by its parent: each heading's text once.
        Assert.Equal(1, Occurrences(prompt, "### docs/storage.md#upload-quota"));
        Assert.Equal(0, Occurrences(prompt, "### docs/storage.md#quota-settings"));
        Assert.Equal(1, Occurrences(prompt, "## Upload quota\n"));
        Assert.Equal(1, Occurrences(prompt, "### Quota settings\n"));
        Assert.Contains("~~~~markdown", prompt);

        Assert.Contains("AC-Q1", prompt);
        Assert.Contains("AC-Q2", prompt);
        Assert.DoesNotContain("AC-D1", prompt);
        Assert.DoesNotContain("AC-S1", prompt);
        Assert.DoesNotContain("AC-X1", prompt);

        Assert.Contains("complete source for this batch", prompt);
        Assert.Contains("source_refs", prompt);
        Assert.Equal("batch", CompileLine(error).GetProperty("mode").GetString());
    }

    [Fact]
    public async Task TheBatchPrompt_NeverAsksToReadDocs_OrNamesTheDeadTools()
    {
        await SectionsOnlyAsync();

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        Assert.Contains("complete source for this batch", prompt);
        foreach (var text in DeadText)
        {
            Assert.DoesNotContain(text, prompt);
        }

        Assert.DoesNotContain("read the relevant documentation", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AWholeDocumentRef_AbsorbsItsSections_AndBringsTheDocumentLevelCriteria()
    {
        await _ws.PlanAsync(AnalysisJson(("Overview", "docs/storage.md"), ("Quota refused", "docs/storage.md#upload-quota")));

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        Assert.True(exit == 0, error);
        Assert.Equal(1, Occurrences(prompt, "### docs/storage.md\n"));
        Assert.Equal(0, Occurrences(prompt, "### docs/storage.md#upload-quota"));
        Assert.Equal(1, Occurrences(prompt, "## Upload quota\n"));
        foreach (var id in new[] { "AC-Q1", "AC-Q2", "AC-D1", "AC-X1" })
        {
            Assert.Contains(id, prompt);
        }

        // AC-X1 names a section the document doesn't have: document-level, with a warning.
        Assert.Contains(GitWorkspace.Strings(CompileLine(error).GetProperty("warnings")), w => w.Contains("AC-X1"));
    }

    [Fact]
    public async Task CountAndFocus_AreTheBatchsCount_OrTheGivenOptions()
    {
        await SectionsOnlyAsync();

        var (_, planned, _) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");
        var (_, overridden, _) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2", "--count", "7", "--focus", "edge cases only");

        // Plan: 3 behaviours, recommended 3, 2 batches → batch 2 gets 1 + 1 = 2.
        Assert.Contains("Generate 2 new manual test cases", planned);
        Assert.Contains("Generate 7 new manual test cases", overridden);
        Assert.Contains("edge cases only", overridden);
    }

    [Fact]
    public async Task TheSameInputs_GiveTheSamePromptBytes()
    {
        await SectionsOnlyAsync();

        var (exit, first, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");
        var (_, second, _) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "2");

        Assert.True(exit == 0, error);
        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SectionsWithoutCriteria_FallBackToTheDocumentsCriteria_WithAWarning()
    {
        await _ws.PlanAsync(AnalysisJson(("Limits apply", "docs/storage.md#limits")));

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        Assert.True(exit == 0, error);
        foreach (var id in new[] { "AC-Q1", "AC-Q2", "AC-D1", "AC-X1" })
        {
            Assert.Contains(id, prompt);
        }

        Assert.DoesNotContain("AC-S1", prompt);
        Assert.Contains(GitWorkspace.Strings(CompileLine(error).GetProperty("warnings")),
            w => w.Contains("no criteria for the batch's sections", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheUnsourcedBatch_CompilesACountModePrompt_WithItsTitlesAsFocus()
    {
        await _ws.PlanAsync(AnalysisJson(("Quota refused", "docs/storage.md#upload-quota"), ("Audit log kept", "docs/nope.md")));

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "unsourced");

        Assert.True(exit == 0, error);
        Assert.Contains("Audit log kept", prompt);
        Assert.DoesNotContain("Quota refused", prompt);
        Assert.Contains("read the relevant documentation", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("complete source for this batch", prompt);
        Assert.Equal("count", CompileLine(error).GetProperty("mode").GetString());
    }

    [Fact]
    public async Task BatchWithFromDescription_IsAUsageError()
    {
        await SectionsOnlyAsync();

        var (exit, output, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1", "--from-description", "x");

        Assert.Equal(1, exit);
        Assert.Equal("", output);
        Assert.Contains("--batch cannot be combined with --from-description.", error);
    }
}
