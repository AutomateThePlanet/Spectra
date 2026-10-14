using Spectra.CLI.Commands.UpdateSkills;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Tests.TestFixtures;
using Spectre.Console;
using static Spectra.CLI.Tests.TestFixtures.ManifestWorkspace;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 096 F1 (FR-002; analyze M1): what <c>update-skills</c> reports — an edited file with a record under "Skipped
/// (customized by user)", an unrecorded different file with the "no record" message, a moved workspace without any
/// warning, and unrecognised foreign keys as one summary warning. Uses the handler's console parameter (new in 096).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateSkillsReportTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public UpdateSkillsReportTests() => _ws.InitWithSkillsAsync().GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private async Task<string> ReportAsync()
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 1000; // no wrapping of long messages
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_ws.Root);
            var exit = await new UpdateSkillsHandler(VerbosityLevel.Normal, OutputFormat.Human, console).ExecuteAsync();
            Assert.Equal(ExitCodes.Success, exit);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }

        return writer.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public async Task EditedFilesWithARecord_AreListedAsCustomized()
    {
        _ws.Append(TemplateFile, "\nHOUSE RULE.\n");
        _ws.Append(ProfileFile, "\n# note\n");
        _ws.Append(CustomizationFile, "\nNotes.\n");

        var report = await ReportAsync();

        var skipped = report[report.IndexOf("Skipped (customized by user)", StringComparison.Ordinal)..];
        Assert.Contains("test-generation.md", skipped);
        Assert.Contains("_default.yaml", skipped);
        Assert.Contains("CUSTOMIZATION.md", skipped);
        Assert.DoesNotContain("no record", report);
    }

    [Fact]
    public async Task AnUnrecordedDifferentFile_GetsTheNoRecordMessage()
    {
        _ws.RekeyManifest(k => _ws.Relative(k) == SkillFile ? null : k);
        _ws.Append(SkillFile, "\nMy own step.\n");

        var report = await ReportAsync();

        Assert.Contains("skipped (no record — it may have been edited); delete it to get the new version", report);
        Assert.Contains("spectra-generate", report);
    }

    [Fact]
    public async Task AnUnrecordedDifferentTemplate_IsToldToResetIt()
    {
        _ws.RekeyManifest(k => _ws.Relative(k) == TemplateFile ? null : k);
        _ws.Append(TemplateFile, "\nHOUSE RULE.\n");

        var report = await ReportAsync();

        Assert.Contains("skipped (no record — it may have been edited); run spectra prompts reset test-generation or delete it to get the new version", report);
    }

    [Fact]
    public async Task AMovedWorkspace_ReportsNoWarning()
    {
        var oldRoot = Path.Combine(Path.GetTempPath(), "spectra-old-location", "ws");
        _ws.RekeyManifest(k => Path.Combine(oldRoot, _ws.Relative(k).Replace('/', Path.DirectorySeparatorChar)));

        var report = await ReportAsync();

        Assert.DoesNotContain("another location", report);
        Assert.DoesNotContain("no record", report);
    }

    [Fact]
    public async Task UnrecognisedForeignKeys_AreOneSummaryWarning()
    {
        var manifest = _ws.Manifest();
        var files = manifest["files"]!.AsObject();
        files[@"D:\elsewhere\notes.txt"] = "abc";
        files[@"D:\elsewhere\more.txt"] = "def";
        _ws.SaveManifest(manifest);

        var report = await ReportAsync();

        Assert.Equal(1, UpdatePromptWorkspace.Count(report, "manifest entries from another location were dropped"));
        Assert.Contains("2 manifest entries from another location were dropped (was the workspace moved?)", report);
    }
}
