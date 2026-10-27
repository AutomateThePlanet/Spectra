using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.UpdatePromptWorkspace;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 098 F7 (FR-017, SC-007): in a group update prompt, a criterion the tests see differently is annotated with each
/// test's own state in that test's block; the shared criteria list keeps the strongest state. An agreeing group's prompt
/// is unchanged. On 986da01 test blocks listed ids only, so TC-201 (its baseline has AC-1) was told AC-1 is "not
/// baselined" because TC-202's entry lacks it.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GroupCriterionStateTests : IDisposable
{
    private const string Storage = "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    /// <summary>TC-201 and TC-202 link Quotas; TC-201 is baselined with AC-1, TC-202 gains AC-1 after; 30GB → 45GB.</summary>
    private async Task<string> DisagreeingGroupAsync()
    {
        _ws.WriteDoc("docs/storage.md", Storage);
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 30GB"));
        _ws.WriteTest("storage", "TC-201", ["docs/storage.md#Quotas"], ["AC-1"]);
        _ws.WriteTest("storage", "TC-202", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-202", ["docs/storage.md#Quotas"], ["AC-1"]);
        _ws.WriteDoc("docs/storage.md", Storage.Replace("30GB", "45GB"));
        return await OneGroupAsync();
    }

    private async Task<string> OneGroupAsync()
    {
        var triage = await _ws.TriageAsync();
        var group = Assert.Single(triage.GetProperty("groups").EnumerateArray());
        Assert.Equal(["TC-201", "TC-202"], GitWorkspace.Strings(group.GetProperty("tests")));
        return group.GetProperty("id").GetString()!;
    }

    private async Task<string> PromptAsync(string group)
    {
        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--group", group);
        Assert.True(exit == 0, error);
        return prompt;
    }

    [Fact]
    public async Task EachTestBlock_ShowsItsOwnState_WhenTheGroupDisagrees()
    {
        var prompt = await PromptAsync(await DisagreeingGroupAsync());

        Assert.Contains("### TEST TC-201\nSections: S1\nCriteria: AC-1 (unchanged)\n", prompt);
        Assert.Contains("### TEST TC-202\nSections: S1\nCriteria: AC-1 (not baselined)\n", prompt);
        Assert.Equal(1, Count(prompt, "- AC-1 (not baselined — compare the test with the current text): Each user MUST get 30GB"));
    }

    [Fact]
    public async Task TheOutput_IsByteIdenticalAcrossRuns()
    {
        var group = await DisagreeingGroupAsync();

        Assert.Equal(await PromptAsync(group), await PromptAsync(group));
    }

    [Fact]
    public async Task AnAgreeingGroup_HasNoAnnotations()
    {
        _ws.WriteDoc("docs/storage.md", Storage);
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 30GB"));
        _ws.WriteTest("storage", "TC-201", ["docs/storage.md#Quotas"], ["AC-1"]);
        _ws.WriteTest("storage", "TC-202", ["docs/storage.md#Quotas"], ["AC-1"]);
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", Storage.Replace("30GB", "45GB"));

        var prompt = await PromptAsync(await OneGroupAsync());

        Assert.Contains("### TEST TC-201\nSections: S1\nCriteria: AC-1\n", prompt);
        Assert.Contains("### TEST TC-202\nSections: S1\nCriteria: AC-1\n", prompt);
    }
}
