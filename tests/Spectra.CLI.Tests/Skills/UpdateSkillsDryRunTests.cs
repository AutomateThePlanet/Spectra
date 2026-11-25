using System.Security.Cryptography;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Commands.UpdateSkills;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Tests.TestFixtures;
using Spectre.Console;
using static Spectra.CLI.Tests.TestFixtures.ManifestWorkspace;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// The global <c>--dry-run</c> on <c>spectra update-skills</c> must write nothing. Up to 2.7.0 the command never read
/// the option: it updated skills, created missing agents and folders, appended to <c>.gitignore</c> and saved the
/// manifest while the user asked for a preview.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateSkillsDryRunTests : IDisposable
{
    private const string OldSkill = "---\nname: spectra-generate\n---\n\nAn older bundled version.\n";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public UpdateSkillsDryRunTests() => _ws.InitWithSkillsAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    /// <summary>Every file (relative path → SHA-256) and every directory under the workspace.</summary>
    private (SortedDictionary<string, string> Files, SortedSet<string> Dirs) Snapshot()
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Directory.EnumerateFiles(_ws.Root, "*", SearchOption.AllDirectories))
        {
            files[Path.GetRelativePath(_ws.Root, f).Replace('\\', '/')] =
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)));
        }

        var dirs = new SortedSet<string>(
            Directory.EnumerateDirectories(_ws.Root, "*", SearchOption.AllDirectories)
                .Select(d => Path.GetRelativePath(_ws.Root, d).Replace('\\', '/')),
            StringComparer.Ordinal);
        return (files, dirs);
    }

    /// <summary>A workspace where a real update-skills has something of every kind to do.</summary>
    private void MakeOutdated()
    {
        // An installed skill at an older bundled version, recorded as SPECTRA's own → would be updated.
        File.WriteAllText(_ws.PathOf(SkillFile), OldSkill);
        var manifest = _ws.Manifest();
        var files = manifest["files"]!.AsObject();
        foreach (var key in files.Select(f => f.Key).Where(k => _ws.Relative(k) == SkillFile).ToList())
        {
            files[key] = Hash(OldSkill);
        }

        _ws.SaveManifest(manifest);

        // A missing subagent → would be created.
        File.Delete(_ws.PathOf(".claude/agents/spectra-repairer.agent.md"));

        // A missing skill folder → a folder would be created.
        Directory.Delete(_ws.PathOf(".claude/skills/spectra-validate"), recursive: true);

        // SPECTRA's .gitignore lines and marker removed → patterns would be offered again.
        var gitignore = _ws.PathOf(".gitignore");
        File.WriteAllLines(gitignore, File.ReadAllLines(gitignore)
            .Where(l => !l.StartsWith(SpectraGitIgnore.MarkerPrefix, StringComparison.Ordinal)
                        && !l.TrimStart().StartsWith(".spectra/", StringComparison.Ordinal)));
    }

    private async Task<(int Exit, string Report)> RunAsync(bool dryRun)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 1000;
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_ws.Root);
            var exit = await new UpdateSkillsHandler(VerbosityLevel.Normal, OutputFormat.Human, console, dryRun: dryRun)
                .ExecuteAsync();
            return (exit, writer.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task DryRun_LeavesTheFileTreeUnchanged_AndReportsWhatWouldChange()
    {
        MakeOutdated();
        var before = Snapshot();

        var (exit, report) = await RunAsync(dryRun: true);

        Assert.Equal(0, exit);
        var after = Snapshot();
        Assert.Equal(before.Files, after.Files);
        Assert.Equal(before.Dirs, after.Dirs);
        Assert.Contains("Would update:", report, StringComparison.Ordinal);
        Assert.Contains("spectra-repairer.agent.md", report, StringComparison.Ordinal);
        Assert.Contains("Would update: .gitignore", report, StringComparison.Ordinal);
        Assert.Contains("Dry run — nothing written.", report, StringComparison.Ordinal);

        // Control: the same workspace without --dry-run does change, so the preview had real work to report.
        var (realExit, _) = await RunAsync(dryRun: false);
        Assert.Equal(0, realExit);
        var real = Snapshot();
        Assert.NotEqual(before.Files, real.Files);
        Assert.Contains(".claude/skills/spectra-validate", real.Dirs);
        Assert.True(File.Exists(_ws.PathOf(".claude/agents/spectra-repairer.agent.md")));
    }
}
