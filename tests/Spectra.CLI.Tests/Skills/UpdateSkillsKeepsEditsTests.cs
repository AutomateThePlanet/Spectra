using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.ManifestWorkspace;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 096 F1 (FR-001–FR-003): <c>spectra update-skills</c> never overwrites a file the user edited — whatever key form
/// the manifest used for it (relative from init, absolute from update-skills, both, or from a moved workspace) and even
/// when it has no record at all. On 23aa08c an edited template, profile or CUSTOMIZATION.md was overwritten (init's
/// relative record was never found), and unrecorded edited files were overwritten.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateSkillsKeepsEditsTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public UpdateSkillsKeepsEditsTests() => _ws.InitWithSkillsAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private async Task UpdateSkillsAsync()
    {
        var (exit, _, error) = await _ws.RunAsync("update-skills");
        Assert.True(exit == 0, error);
    }

    private byte[] Bytes(string relative) => File.ReadAllBytes(_ws.PathOf(relative));

    [Fact]
    public async Task EditedTemplateProfileAndCustomization_AreNeverOverwritten()
    {
        _ws.Append(TemplateFile, "\nHOUSE RULE: imperative steps.\n");
        _ws.Append(ProfileFile, "\n# team note\n");
        _ws.Append(CustomizationFile, "\nTeam notes.\n");
        var before = new[] { TemplateFile, ProfileFile, CustomizationFile }.ToDictionary(f => f, Bytes);

        await UpdateSkillsAsync();

        foreach (var (file, bytes) in before)
        {
            Assert.Equal(bytes, Bytes(file));
        }
    }

    [Fact]
    public async Task ANoRecordFile_EqualToTheBundledContent_IsRecordedAgain()
    {
        _ws.RekeyManifest(k => _ws.Relative(k) == SkillFile ? null : k);
        var before = Bytes(SkillFile);

        await UpdateSkillsAsync();

        Assert.Equal(before, Bytes(SkillFile));
        Assert.True(_ws.ManifestFiles().ContainsKey(SkillFile), "the skill is recorded again under its normalised key");
    }

    [Fact]
    public async Task ANoRecordFile_ThatDiffers_IsNeverOverwritten()
    {
        _ws.RekeyManifest(k => _ws.Relative(k) == SkillFile ? null : k);
        _ws.Append(SkillFile, "\nMy own step.\n");
        var before = Bytes(SkillFile);

        await UpdateSkillsAsync();

        Assert.Equal(before, Bytes(SkillFile));
    }

    [Fact]
    public async Task APre096Manifest_IsNormalised_AndNoEditedFileIsOverwritten()
    {
        // As init and update-skills wrote it: absolute skill/agent keys, '\' relative template keys; plus a foreign key.
        _ws.RekeyManifest(k => Path.IsPathRooted(k) ? k : k.Replace('/', '\\'));
        var manifest = _ws.Manifest();
        manifest["files"]!.AsObject()[@"D:\elsewhere\x.md"] = "abc";
        _ws.SaveManifest(manifest);
        _ws.Append(TemplateFile, "\nHOUSE RULE.\n");
        var before = Bytes(TemplateFile);

        await UpdateSkillsAsync();

        Assert.Equal(before, Bytes(TemplateFile));
        var keys = _ws.ManifestFiles().Select(f => f.Key).ToList();
        Assert.All(keys, k => Assert.False(Path.IsPathRooted(k) || k.Contains('\\'), $"not normalised: {k}"));
        Assert.DoesNotContain(keys, k => k.Contains("elsewhere"));
    }

    // ---- duplicate keys (analyze H1) ----

    /// <summary>Init's relative key = hash(v1); update-skills' absolute key = hash(v2); the file content is v2.</summary>
    private void TwoRecords(string v1, string v2)
    {
        File.WriteAllText(_ws.PathOf(TemplateFile), v2);
        var manifest = _ws.Manifest();
        var files = manifest["files"]!.AsObject();
        foreach (var key in files.Select(f => f.Key).Where(k => _ws.Relative(k) == TemplateFile).ToList())
        {
            files.Remove(key);
        }

        files[@".spectra\prompts\test-generation.md"] = Hash(v1);
        files[_ws.PathOf(TemplateFile)] = Hash(v2);
        _ws.SaveManifest(manifest);
    }

    [Fact]
    public async Task DuplicateKeys_AFileMatchingEitherHash_IsUnedited_AndUpdated()
    {
        TwoRecords("old template v1\n", "old template v2\n");
        var bundled = Spectra.CLI.Prompts.BuiltInTemplates.GetRawContent("test-generation")!;

        await UpdateSkillsAsync();

        Assert.Equal(bundled, File.ReadAllText(_ws.PathOf(TemplateFile)));
    }

    [Fact]
    public async Task DuplicateKeys_AnEditedFile_IsCustomized_AndKept()
    {
        TwoRecords("old template v1\n", "old template v2\n");
        File.WriteAllText(_ws.PathOf(TemplateFile), "my own template\n");

        await UpdateSkillsAsync();

        Assert.Equal("my own template\n", File.ReadAllText(_ws.PathOf(TemplateFile)));
    }

    [Fact]
    public async Task DuplicateKeys_AreSavedAsOneNormalisedEntry()
    {
        TwoRecords("old template v1\n", "old template v2\n");

        await UpdateSkillsAsync();

        Assert.Single(_ws.ManifestFiles(), f => _ws.Relative(f.Key) == TemplateFile);
        Assert.True(_ws.ManifestFiles().ContainsKey(TemplateFile));
    }

    // ---- a moved workspace (analyze M1) ----

    [Fact]
    public async Task AMovedWorkspace_IsReRooted_AndAnEditedFileIsKept()
    {
        var oldRoot = Path.Combine(Path.GetTempPath(), "spectra-old-location", "ws");
        _ws.RekeyManifest(k => Path.Combine(oldRoot, _ws.Relative(k).Replace('/', Path.DirectorySeparatorChar)));
        _ws.Append(TemplateFile, "\nHOUSE RULE.\n");
        var before = Bytes(TemplateFile);

        await UpdateSkillsAsync();

        Assert.Equal(before, Bytes(TemplateFile));
        var keys = _ws.ManifestFiles().Select(f => f.Key).ToList();
        Assert.Contains(TemplateFile, keys);
        Assert.Contains(SkillFile, keys);
        Assert.DoesNotContain(keys, k => k.Contains("spectra-old-location"));
    }

    [Fact]
    public async Task AnUnrecognisedForeignKey_IsDropped()
    {
        var manifest = _ws.Manifest();
        manifest["files"]!.AsObject()[@"D:\elsewhere\notes.txt"] = "abc";
        _ws.SaveManifest(manifest);

        await UpdateSkillsAsync();

        Assert.DoesNotContain(_ws.ManifestFiles(), f => f.Key.Contains("notes.txt"));
    }
}
