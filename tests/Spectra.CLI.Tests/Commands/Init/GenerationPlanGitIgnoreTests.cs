using System.Text.Json.Nodes;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Init;

/// <summary>
/// Spec 094 T8 (FR-008; R10): the generation plan folder is scratch. The real <c>spectra init</c> gitignores it and
/// records it as offered; <c>update-skills</c> offers it once to a workspace from 090–093 (a recorded list without it) and
/// to a pre-090 one (no record), never again, and never re-adds it after the user removed it. On 55b541f the entry
/// didn't exist. Spec 096 (FR-009): the record is a <c># spectra-offered:</c> line in <c>.gitignore</c>; a 090–093 or
/// pre-090 workspace has no marker line, and its per-clone record (if any) is migrated.
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

    private string ManifestPath => _ws.PathOf(".spectra/skills-manifest.json");

    private JsonObject Manifest() =>
        File.Exists(ManifestPath) ? JsonNode.Parse(File.ReadAllText(ManifestPath))!.AsObject() : new JsonObject { ["files"] = new JsonObject() };

    private void SaveManifest(JsonObject manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        File.WriteAllText(ManifestPath, manifest.ToJsonString());
    }

    /// <summary>Spec 096: a workspace from before 096 has no marker line.</summary>
    private void RemoveMarkers() =>
        File.WriteAllText(GitIgnorePath, string.Concat(File.ReadAllText(GitIgnorePath).Replace("\r\n", "\n").Split('\n')
            .Where(l => !l.StartsWith(SpectraGitIgnore.MarkerPrefix, StringComparison.Ordinal)).Select(l => l + "\n")).TrimEnd('\n') + "\n");

    private IReadOnlyList<string> Marked() =>
        SpectraGitIgnore.Marked(File.ReadAllText(GitIgnorePath).Replace("\r\n", "\n").Split('\n'));

    /// <summary>
    /// A workspace from 090–093: the entry absent from .gitignore, no marker line, and the per-clone record (the list
    /// as 093 wrote it, before 094's entry and 096's two entries).
    /// </summary>
    private void MakeA093Workspace()
    {
        RemoveEntry();
        RemoveMarkers();
        var manifest = Manifest();
        manifest["gitignore_offered"] = new JsonArray(SpectraGitIgnore.Patterns
            .Where(p => p is not (Entry or ".spectra/verdicts/" or ".spectra/dropped-tests.json"))
            .Select(p => (JsonNode?)JsonValue.Create(p)).ToArray());
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
        Assert.Contains(Entry, Marked());
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
        RemoveMarkers();
        var manifest = Manifest();
        manifest.Remove("gitignore_offered");
        SaveManifest(manifest);

        await UpdateSkillsAsync();

        Assert.Equal(1, EntryLines());
    }
}
