using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.ManifestWorkspace;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 098 F4 (FR-010, FR-011, SC-004): a pre-096 manifest can hold one file under two keys — init's relative key (an
/// older hash) and update-skills' absolute key (the hash SPECTRA wrote last). When the file is skipped as customized,
/// the saved record must be the newest one, so a user who restores that content gets it updated next time. When the
/// newest can't be told apart, every candidate is saved. On 986da01 the saved record was the first hash in ordinal key
/// order — init's older one — and the restored file was called customized again.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ManifestNewestTests : IDisposable
{
    private const string V1 = "---\nname: spectra-generate\n---\n\nVersion one, as init wrote it.\n";
    private const string V2 = "---\nname: spectra-generate\n---\n\nVersion two, as update-skills wrote it.\n";
    private const string Edited = "---\nname: spectra-generate\n---\n\nThe team's own version.\n";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public ManifestNewestTests() => _ws.InitWithSkillsAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    /// <summary>Records <see cref="SkillFile"/> under each (key, content) pair instead of its own record.</summary>
    private void RecordUnder(params (string Key, string Content)[] records)
    {
        var manifest = _ws.Manifest();
        var files = manifest["files"]!.AsObject();
        foreach (var key in files.Select(f => f.Key).Where(k => _ws.Relative(k) == SkillFile).ToList())
        {
            files.Remove(key);
        }

        foreach (var (key, content) in records)
        {
            files[key] = Hash(content);
        }

        _ws.SaveManifest(manifest);
    }

    private async Task<string> UpdateSkillsAsync()
    {
        var (exit, report) = await _ws.UpdateSkillsReportAsync();
        Assert.True(exit == 0, report);
        return report;
    }

    private string Read() => File.ReadAllText(_ws.PathOf(SkillFile));

    private static bool IsSkipped(string report) =>
        report.IndexOf("Skipped (customized by user)", StringComparison.Ordinal) is var at and >= 0
        // Spec 101: the skill's own SKILL.md, not its reference.md (which lives in the same folder).
        && report[at..].Replace('\\', '/').Contains("spectra-generate/SKILL.md", StringComparison.Ordinal);

    [Fact]
    public async Task RelativeV1AndAbsoluteV2_Edited_IsSkipped_ThenRestoringV2_IsUpdated()
    {
        RecordUnder(
            (SkillFile.Replace('/', '\\'), V1),
            (_ws.PathOf(SkillFile), V2));
        File.WriteAllText(_ws.PathOf(SkillFile), Edited);

        Assert.True(IsSkipped(await UpdateSkillsAsync()), "an edited file is skipped");
        Assert.Equal(Edited, Read());
        Assert.Equal(Hash(V2), _ws.ManifestFiles()[SkillFile]!.GetValue<string>());

        File.WriteAllText(_ws.PathOf(SkillFile), V2);
        var report = await UpdateSkillsAsync();

        Assert.False(IsSkipped(report), report);
        Assert.Equal(Spectra.CLI.Skills.SkillContent.Generate, Read());
    }

    [Fact]
    public async Task TwoRecordsOfTheSameRank_AreBothKept_AndEitherCountsAsUnedited()
    {
        var oldA = "D:/old-a/ws/" + SkillFile;
        var oldB = "D:/old-b/ws/" + SkillFile;
        RecordUnder((oldA, V1), (oldB, V2));
        File.WriteAllText(_ws.PathOf(SkillFile), Edited);

        Assert.True(IsSkipped(await UpdateSkillsAsync()));
        var candidates = _ws.Manifest()["candidates"]?[SkillFile]?.AsArray().Select(h => h!.GetValue<string>()).ToHashSet();
        Assert.NotNull(candidates);
        Assert.Contains(Hash(V1), candidates!);
        Assert.Contains(Hash(V2), candidates!);

        File.WriteAllText(_ws.PathOf(SkillFile), V2);
        await UpdateSkillsAsync();

        Assert.Equal(Spectra.CLI.Skills.SkillContent.Generate, Read());
        Assert.Null(_ws.Manifest()["candidates"]?[SkillFile]);
    }

    [Fact]
    public async Task AManifestWithOneKeyPerFile_IsSavedWithoutCandidates()
    {
        await UpdateSkillsAsync();

        Assert.Null(_ws.Manifest()["candidates"]);
    }
}
