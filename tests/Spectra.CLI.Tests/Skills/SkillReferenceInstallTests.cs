using Spectra.CLI.Skills;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.ManifestWorkspace;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 101 F2 (FR-012; US6): <c>init</c> and <c>update-skills</c> install, record and refresh each skill's
/// <c>reference.md</c> under the 096 rules — written when missing (listed "Updated"), never overwritten when edited,
/// kept across a moved workspace, not installed with <c>--skip-skills</c>. Disk-only checks, so the rows compile on
/// 6a4c41a, where no reference file existed.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SkillReferenceInstallTests : IDisposable
{
    private const string GenerateReference = ".claude/skills/spectra-generate/reference.md";
    private const string UpdateReference = ".claude/skills/spectra-update/reference.md";
    private const string UpdateSkill = ".claude/skills/spectra-update/SKILL.md";
    private const string UpdaterAgent = ".claude/agents/spectra-updater.agent.md";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    private bool HasKey(string relative) => _ws.ManifestFiles().Any(f => _ws.Relative(f.Key) == relative);

    private static readonly string[] Headers =
        ["Updated:", "Unchanged:", "Skipped (customized by user):", "skipped (no record", "manifest entries from another location"];

    /// <summary>The report lines between <paramref name="header"/> and the next section header.</summary>
    private static List<string> Section(string report, string header)
    {
        var start = report.IndexOf(header, StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        start += header.Length;
        var end = Headers.Select(h => report.IndexOf(h, start, StringComparison.Ordinal))
            .Where(i => i >= 0).DefaultIfEmpty(report.Length).Min();
        return report[start..end].Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
    }

    [Fact]
    public async Task Init_InstallsAndRecordsBothReferenceFiles()
    {
        await _ws.InitWithSkillsAsync();

        Assert.True(File.Exists(_ws.PathOf(GenerateReference)));
        Assert.True(File.Exists(_ws.PathOf(UpdateReference)));
        Assert.True(HasKey(GenerateReference));
        Assert.True(HasKey(UpdateReference));
    }

    [Fact]
    public async Task InitWithSkipSkills_InstallsNoReferenceFile()
    {
        await TemplateWorkspaces.InitAsync(_ws, skipSkills: true);

        Assert.False(File.Exists(_ws.PathOf(GenerateReference)));
        Assert.False(File.Exists(_ws.PathOf(UpdateReference)));
    }

    [Fact]
    public async Task APre101Workspace_GetsBothReferenceFiles_ListedAsUpdated()
    {
        await _ws.InitWithSkillsAsync();
        File.Delete(_ws.PathOf(GenerateReference));
        File.Delete(_ws.PathOf(UpdateReference));
        _ws.RekeyManifest(k => _ws.Relative(k) is GenerateReference or UpdateReference ? null : k);

        var (exit, report) = await _ws.UpdateSkillsReportAsync();

        Assert.Equal(0, exit);
        Assert.True(File.Exists(_ws.PathOf(GenerateReference)));
        Assert.True(File.Exists(_ws.PathOf(UpdateReference)));
        var updated = Section(report, "Updated:");
        Assert.Contains(updated, l => l.Replace('\\', '/').EndsWith(GenerateReference, StringComparison.Ordinal));
        Assert.Contains(updated, l => l.Replace('\\', '/').EndsWith(UpdateReference, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnEditedSkill_IsKept_AndItsReferenceFileIsStillWritten()
    {
        await _ws.InitWithSkillsAsync();
        File.Delete(_ws.PathOf(UpdateReference));
        _ws.RekeyManifest(k => _ws.Relative(k) == UpdateReference ? null : k);
        _ws.Append(UpdateSkill, "\nHOUSE RULE.\n");
        var edited = File.ReadAllText(_ws.PathOf(UpdateSkill));

        var (_, report) = await _ws.UpdateSkillsReportAsync();

        Assert.Equal(edited, File.ReadAllText(_ws.PathOf(UpdateSkill)));
        Assert.Contains(Section(report, "Skipped (customized by user):"),
            l => l.Replace('\\', '/').EndsWith(UpdateSkill, StringComparison.Ordinal));
        Assert.True(File.Exists(_ws.PathOf(UpdateReference)));
    }

    [Fact]
    public async Task AnEditedAgent_IsKept_WithItsOldFrontmatter()
    {
        await _ws.InitWithSkillsAsync();
        _ws.Append(UpdaterAgent, "\nHOUSE RULE.\n");
        var edited = File.ReadAllText(_ws.PathOf(UpdaterAgent));

        var (_, report) = await _ws.UpdateSkillsReportAsync();

        Assert.Equal(edited, File.ReadAllText(_ws.PathOf(UpdaterAgent)));
        Assert.Contains(Section(report, "Skipped (customized by user):"),
            l => l.Replace('\\', '/').EndsWith(UpdaterAgent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnEditedReferenceFile_IsKept_AndReportedAsCustomized()
    {
        await _ws.InitWithSkillsAsync();
        _ws.Append(GenerateReference, "\nTEAM NOTE.\n");
        var edited = File.ReadAllText(_ws.PathOf(GenerateReference));

        var (_, report) = await _ws.UpdateSkillsReportAsync();

        Assert.Equal(edited, File.ReadAllText(_ws.PathOf(GenerateReference)));
        Assert.Contains(Section(report, "Skipped (customized by user):"),
            l => l.Replace('\\', '/').EndsWith(GenerateReference, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMovedWorkspace_KeepsTheReferenceKeys_WithoutAWarning()
    {
        await _ws.InitWithSkillsAsync();
        var oldRoot = Path.Combine(Path.GetTempPath(), "spectra-old-location-101", "ws");
        _ws.RekeyManifest(k => Path.Combine(oldRoot, _ws.Relative(k).Replace('/', Path.DirectorySeparatorChar)));

        var (_, report) = await _ws.UpdateSkillsReportAsync();

        Assert.DoesNotContain("another location", report);
        Assert.True(HasKey(GenerateReference));
        Assert.True(HasKey(UpdateReference));
    }

    [Fact]
    public void OnlyTheTwoAuthoringSkills_HaveAReferenceFile()
    {
        Assert.Equal(["spectra-generate", "spectra-update"], SkillReferenceContent.All.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ReferenceFiles_AreNotSkills()
    {
        Assert.Equal(16, SkillContent.All.Count);
        Assert.DoesNotContain(SkillContent.All.Keys, k => k.Contains("reference", StringComparison.OrdinalIgnoreCase));
    }
}
