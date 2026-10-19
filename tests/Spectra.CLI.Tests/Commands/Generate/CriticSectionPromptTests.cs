using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 101 F1 (FR-001–FR-006; US1): <c>compile-critic-prompt</c> gives the critic the sections a test cites, and
/// <c>--full-docs</c> gives exactly what <c>6a4c41a</c> gave (the golden captured from that build). The critic still sees
/// no criteria; refusals, the JSON shape and the freshness record are unchanged. On 6a4c41a the cited Section 30 was cut
/// off (each document stops at 8,000 characters) and <c>--full-docs</c> didn't exist.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CriticSectionPromptTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public CriticSectionPromptTests() => _ws.GoldenWorkspace();

    public void Dispose() => _ws.Dispose();

    private async Task<(int Exit, string Out, string Err)> CompileAsync(string id, params string[] extra)
    {
        var (exit, output, error) = await _ws.RunAsync(["ai", "compile-critic-prompt", "--suite", "s", "--test", id, .. extra]);
        return (exit, CriticSourceWorkspace.Lf(output), error);
    }

    private async Task<string> PromptAsync(string id, params string[] extra)
    {
        var (exit, output, error) = await CompileAsync(id, extra);
        Assert.True(exit == 0, $"exit {exit}: {error}");
        return output;
    }

    [Fact]
    public async Task SectionMode_GivesTheCitedSections_EvenPastTheCut()
    {
        var prompt = await PromptAsync("TC-001");

        Assert.Contains("Section 30 paragraph line 3", prompt);
        Assert.Contains("Section 2 paragraph line 5", prompt);
        Assert.DoesNotContain("## Section 29", prompt);
        Assert.DoesNotContain("## Section 31", prompt);
        Assert.DoesNotContain("## Section 3\n", prompt);
        Assert.Contains("Short document body line.", prompt);
    }

    [Fact]
    public async Task AnUnmatchedAnchor_GivesTheWholeDocument_WithANote()
    {
        _ws.CitingTest("TC-002", "docs/long.md#removed");

        var prompt = await PromptAsync("TC-002");

        Assert.Contains("> Note: the anchor \"#removed\" was not found in this document, so the whole document is given.", prompt);
        Assert.Contains("## Section 1\n", prompt);
    }

    [Fact]
    public async Task FullDocs_IsByteIdenticalTo6a4c41a()
    {
        Assert.Equal(CriticSourceWorkspace.Golden(), await PromptAsync("TC-001", "--full-docs"));
    }

    [Fact]
    public async Task ExplicitDocs_AreUsedAsToday_WithOrWithoutFullDocs()
    {
        var without = await PromptAsync("TC-001", "--docs", "docs/long.md");
        var with = await PromptAsync("TC-001", "--docs", "docs/long.md", "--full-docs");

        Assert.Equal(without, with);
        Assert.Contains("## Section 1\n", without);
    }

    [Fact]
    public async Task SectionMode_IsDeterministic()
    {
        Assert.Equal(await PromptAsync("TC-001"), await PromptAsync("TC-001"));
    }

    [Fact]
    public async Task FullDocsMode_IsDeterministic()
    {
        Assert.Equal(await PromptAsync("TC-001", "--full-docs"), await PromptAsync("TC-001", "--full-docs"));
    }

    [Fact]
    public async Task SectionMode_HoldsNoCriteria()
    {
        _ws.MarkerCriteria();

        Assert.DoesNotContain("UNIQUECRITERIONMARKER", await PromptAsync("TC-001"));
    }

    [Fact]
    public async Task FullDocsMode_HoldsNoCriteria()
    {
        _ws.MarkerCriteria();

        Assert.DoesNotContain("UNIQUECRITERIONMARKER", await PromptAsync("TC-001", "--full-docs"));
    }

    [Fact]
    public async Task AMalformedCriteriaFile_DoesNotAffectThePrompt()
    {
        _ws.MalformedCriteria();

        Assert.Contains("**Path**: docs/long.md", await PromptAsync("TC-001"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATestWithoutRefs_GetsNoDocuments_AsToday(bool fullDocs)
    {
        _ws.CitingTest("TC-003");

        var prompt = await PromptAsync("TC-003", fullDocs ? ["--full-docs"] : []);

        Assert.Contains("*No relevant documentation provided.*", prompt);
        Assert.DoesNotContain("**Path**:", prompt);
    }

    [Fact]
    public async Task JsonShape_IsUnchanged()
    {
        var json = BaselineTestWorkspace.Json((await CompileAsync("TC-001", "--output-format", "json")).Out);

        var entry = Assert.Single(json.GetProperty("prompts").EnumerateArray());
        Assert.Equal("TC-001", entry.GetProperty("id").GetString());
        Assert.Contains("**Path**: docs/long.md", entry.GetProperty("prompt").GetString());
    }

    [Fact]
    public async Task AnUnknownId_StillExits1()
    {
        Assert.Equal(1, (await CompileAsync("TC-999")).Exit);
    }

    [Fact]
    public async Task AMissingRefFile_StillExits1_WithTheSameMessage()
    {
        _ws.CitingTest("TC-004", "docs/gone.md#x");

        var (exit, _, error) = await CompileAsync("TC-004");

        Assert.Equal(1, exit);
        Assert.Contains("Source ref not found: docs/gone.md#x", error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheFreshnessRecord_IsWritten_InBothModes(bool fullDocs)
    {
        await PromptAsync("TC-001", fullDocs ? ["--full-docs"] : []);

        Assert.True(File.Exists(_ws.VerdictRecordPath("TC-001")));
    }

    [Fact]
    public async Task AnAmbiguousAnchor_UsesTheFirstHeading_AndWarnsOnStderr()
    {
        _ws.WriteDoc("docs/dup.md", "# Doc\n\n## Setup\n\nFirst setup.\n\n## Setup\n\nSecond setup.\n");
        _ws.CitingTest("TC-005", "docs/dup.md#setup");

        var (exit, output, error) = await CompileAsync("TC-005");

        Assert.Equal(0, exit);
        Assert.Contains("First setup.", output);
        Assert.DoesNotContain("Second setup.", output);
        Assert.Contains("Warning: docs/dup.md#setup: more than one heading shares the anchor key 'setup'; the first was used", error);
    }
}
