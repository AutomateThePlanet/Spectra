using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 094 T8 (FR-008; R10): the generation plan folder is scratch. The real <c>spectra init</c> gitignores it and
/// records it as offered; <c>update-skills</c> offers it once to a workspace from 090–093 (a recorded list without it) and
/// to a pre-090 one (no record), never again, and never re-adds it after the user removed it. On 55b541f the entry
/// didn't exist.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GenerationPlanGitIgnoreTests : IDisposable
{
    private const string Entry = ".spectra/generation-plan/";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public GenerationPlanGitIgnoreTests() => _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private string GitIgnorePath => _ws.PathOf(".gitignore");

    private int EntryLines() =>
        File.ReadAllText(GitIgnorePath).Replace("\r\n", "\n").Split('\n').Count(l => l.Trim() == Entry);

    private void RemoveEntry() =>
        File.WriteAllText(GitIgnorePath, string.Concat(File.ReadAllText(GitIgnorePath).Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Trim() != Entry).Select(l => l + "\n")).TrimEnd('\n') + "\n");

    private JsonObject Manifest() => JsonNode.Parse(File.ReadAllText(_ws.PathOf(".spectra/skills-manifest.json")))!.AsObject();

    private void SaveManifest(JsonObject manifest) =>
        File.WriteAllText(_ws.PathOf(".spectra/skills-manifest.json"), manifest.ToJsonString());

    /// <summary>A workspace from 090–093: the entry absent from .gitignore and from the recorded offered list.</summary>
    private void MakeA093Workspace()
    {
        RemoveEntry();
        var manifest = Manifest();
        var offered = manifest["gitignore_offered"]!.AsArray();
        foreach (var node in offered.Where(n => n!.GetValue<string>() == Entry).ToList())
        {
            offered.Remove(node);
        }

        SaveManifest(manifest);
    }

    private async Task UpdateSkillsAsync()
    {
        var (exit, _, error) = await _ws.RunAsync("update-skills");
        Assert.True(exit == 0, error);
    }

    [Fact]
    public void TheRealInit_GitignoresThePlanFolder_AndRecordsItAsOffered()
    {
        Assert.Equal(1, EntryLines());
        Assert.Contains(Manifest()["gitignore_offered"]!.AsArray(), n => n!.GetValue<string>() == Entry);
    }

    [Fact]
    public async Task UpdateSkills_OffersItOnceToA093Workspace()
    {
        MakeA093Workspace();

        await UpdateSkillsAsync();
        Assert.Equal(1, EntryLines());

        await UpdateSkillsAsync();
        Assert.Equal(1, EntryLines());
    }

    [Fact]
    public async Task UpdateSkills_NeverReAddsItAfterTheUserRemovedIt()
    {
        MakeA093Workspace();
        await UpdateSkillsAsync();
        Assert.Equal(1, EntryLines());
        RemoveEntry();

        await UpdateSkillsAsync();

        Assert.Equal(0, EntryLines());
    }

    [Fact]
    public async Task UpdateSkills_OffersItToAPre090Workspace_WithoutARecord()
    {
        RemoveEntry();
        var manifest = Manifest();
        manifest.Remove("gitignore_offered");
        SaveManifest(manifest);

        await UpdateSkillsAsync();

        Assert.Equal(1, EntryLines());
    }
}
