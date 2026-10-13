using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Baseline;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 095 T4 (FR-008, FR-010): no <c>compile-prompt --batch</c> prompt names a behaviour an existing test covers, and
/// a plan written by 094 (no coverage information) still compiles — with a warning to re-run the analysis. On db20415
/// covered behaviours were batched, and a 094 plan compiled without a warning (the compatibility row is a guard that
/// passes on both).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class PlanCoverageCompileTests : IDisposable
{
    private const string PredatesWarning =
        "the generation plan predates per-behaviour coverage; re-run the analysis for corrected counts";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public PlanCoverageCompileTests() => SetUpAsync(_ws).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private static readonly string[] CoveredTitles = ["Quota refused above 30 GB", "Links expire after 7 days", "Audit log kept 90 days"];

    [Fact]
    public async Task NoBatchPrompt_NamesACoveredBehaviour()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#upload-quota"], title: CoveredTitles[0]);
        _ws.WriteTest("storage", "TC-102", ["docs/sharing.md#links"], title: CoveredTitles[1]);
        _ws.WriteTest("storage", "TC-103", ["docs/storage.md"], title: CoveredTitles[2]);
        var (exit, output, error) = await _ws.IngestAnalysisAsync(AnalysisJson(
            (CoveredTitles[0], "docs/storage.md#upload-quota"),
            ("Quota shown in settings", "docs/storage.md#quota-settings"),
            (CoveredTitles[1], "docs/sharing.md#links"),
            ("Share link opens the file preview", "docs/sharing.md#links"),
            (CoveredTitles[2], "docs/nope.md"),
            ("Restore keeps the sharing settings", "docs/nope2.md")));
        Assert.True(exit == 0, error);

        var ids = BaselineTestWorkspace.Json(output).GetProperty("batches").EnumerateArray().Select(b => b.GetProperty("id").GetString()!).ToList();
        Assert.Contains("unsourced", ids);
        foreach (var id in ids)
        {
            var (compileExit, prompt, compileError) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", id);
            Assert.True(compileExit == 0, compileError);
            foreach (var title in CoveredTitles)
            {
                Assert.DoesNotContain(title, prompt);
            }
        }
    }

    /// <summary>The 094 plan of <see cref="Plan094"/> with the workspace's real section fingerprints.</summary>
    private void Write094Plan()
    {
        string Fingerprint(string doc, string anchor) =>
            SourceFingerprint.OfSection(SectionExtractor.Extract(File.ReadAllText(_ws.PathOf(doc)), anchor).RawText!);

        var sections = "\"sections\": {\n"
            + $"    \"docs/sharing.md#links\": \"{Fingerprint("docs/sharing.md", "links")}\",\n"
            + $"    \"docs/storage.md#upload-quota\": \"{Fingerprint("docs/storage.md", "upload-quota")}\"\n  }}";
        Directory.CreateDirectory(_ws.PathOf(".spectra/generation-plan"));
        File.WriteAllText(_ws.PathOf(".spectra/generation-plan/storage.json"),
            Plan094.Replace("\"sections\": {}", sections));
    }

    [Fact]
    public async Task A094Plan_StillCompiles()
    {
        Write094Plan();

        var (exit, prompt, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        Assert.True(exit == 0, error);
        Assert.Contains("Links expire", prompt);
    }

    [Fact]
    public async Task A094Plan_CompilesWithAWarningToReRunTheAnalysis()
    {
        Write094Plan();

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "1");

        Assert.True(exit == 0, error);
        Assert.Contains(PredatesWarning, GitWorkspace.Strings(CompileLine(error).GetProperty("warnings")));
    }
}
