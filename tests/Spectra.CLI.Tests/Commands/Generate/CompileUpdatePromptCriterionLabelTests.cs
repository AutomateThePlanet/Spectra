using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 089 T4 (F4, FR-016–FR-017), command level: a linked criterion with no baseline value to compare with is labelled
/// "not baselined", never shown as unchanged. Baselined-and-equal stays unmarked, baselined-and-different is "changed
/// since baseline"; across a group the strongest label wins. On 6ea7520 the not-baselined rows rendered "- AC-1: …".
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CompileUpdatePromptCriterionLabelTests : IDisposable
{
    private const string Quotas = "docs/storage.md#Quotas";
    private const string NotBaselined = "- AC-1 (not baselined — compare the test with the current text): Each user MUST get 30GB";

    private readonly BaselineTestWorkspace _ws = new();

    public CompileUpdatePromptCriterionLabelTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n");
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 30GB"));
    }

    public void Dispose() => _ws.Dispose();

    private async Task<string> PromptAsync(params string[] args)
    {
        var (exit, prompt, error) = await _ws.CompileAsync(args);
        Assert.True(exit == 0, error);
        return prompt;
    }

    [Fact]
    public async Task NoBaselineAtAll_IsNotBaselined()
    {
        _ws.WriteTest("storage", "TC-101", [Quotas], ["AC-1"]);

        var prompt = await PromptAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.Contains(NotBaselined, prompt);
        Assert.DoesNotContain("- AC-1: ", prompt);
    }

    [Fact]
    public async Task AnEntryWithoutTheCriterion_IsNotBaselined()
    {
        _ws.WriteTest("storage", "TC-101", [Quotas]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-101", [Quotas], ["AC-1"]); // AC-1 linked after the baseline

        var prompt = await PromptAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.Contains(NotBaselined, prompt);
    }

    [Fact]
    public async Task BaselinedAndEqual_IsUnmarked()
    {
        _ws.WriteTest("storage", "TC-101", [Quotas], ["AC-1"]);
        await _ws.InitBaselineAsync();

        var prompt = await PromptAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.Contains("- AC-1: Each user MUST get 30GB", prompt);
        Assert.DoesNotContain("not baselined — compare", prompt);
    }

    [Fact]
    public async Task BaselinedAndDifferent_IsChangedSinceBaseline()
    {
        _ws.WriteTest("storage", "TC-101", [Quotas], ["AC-1"]);
        await _ws.InitBaselineAsync();
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 45GB"));

        var prompt = await PromptAsync("--suite", "storage", "--test-id", "TC-101");

        Assert.Contains("- AC-1 (changed since baseline): Each user MUST get 45GB", prompt);
    }

    [Fact]
    public async Task InAGroup_NotBaselinedOutranksUnchanged_AndThePromptIsByteIdentical()
    {
        // TC-101 baselined with AC-1 (equal); TC-102 baselined without it, then linked. 30GB → 45GB puts both in one
        // LOAD group with the same changed section.
        _ws.WriteTest("storage", "TC-101", [Quotas], ["AC-1"]);
        _ws.WriteTest("storage", "TC-102", [Quotas]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-102", [Quotas], ["AC-1"]);
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets **45GB** of storage space.\n");
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-101");
        Assert.Contains("TC-102", group.GetProperty("tests").EnumerateArray().Select(t => t.GetString()));
        var id = group.GetProperty("id").GetString()!;

        var first = await PromptAsync("--suite", "storage", "--group", id);
        var second = await PromptAsync("--suite", "storage", "--group", id);

        Assert.Equal(1, UpdatePromptWorkspace.Count(first, NotBaselined));
        Assert.Equal(first, second);
    }
}
