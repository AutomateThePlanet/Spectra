using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Baseline;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 094 T3 (FR-001–FR-003, FR-007): the analysis ingest resolves every behaviour's source to a section, saves the
/// generation plan at <c>.spectra/generation-plan/&lt;suite&gt;.json</c> and reports the batches and the unresolved
/// behaviours. On 55b541f it reported counts only and wrote nothing.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GenerationPlanIngestTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public GenerationPlanIngestTests() => GenerationPlanWorkspace.SetUpAsync(_ws, criteria: false).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private static string AnalysisJson(params (string Title, string Source)[] behaviors) => GenerationPlanWorkspace.AnalysisJson(behaviors);

    private const string StorageDoc = GenerationPlanWorkspace.StorageDoc;

    /// <summary>B1–B9: two spellings of one section, a whole doc, another doc, three unresolved, an ambiguous anchor, a '|' title.</summary>
    internal static string Mixed => AnalysisJson(
        ("Upload above quota is refused", "docs/storage.md#Upload quota"),
        ("Upload at exactly the quota succeeds", "docs/storage.md#upload-quota"),
        ("Storage overview", "docs/storage.md"),
        ("Share links expire", "docs/sharing.md#links"),
        ("Audit log", "docs/nope.md"),
        ("Missing section", "docs/storage.md#missing"),
        ("No source", ""),
        ("Limits apply", "docs/storage.md#limits"),
        ("Upload | download", "docs/sharing.md#links"));

    private Task<(int Exit, string Out, string Err)> IngestAsync(string analysis, string suite = "storage") =>
        _ws.IngestAnalysisAsync(analysis, suite);

    private string PlanPath(string suite = "storage") => _ws.PathOf($".spectra/generation-plan/{suite}.json");

    [Fact]
    public async Task TheIngest_SavesThePlan_AndReportsBatchesAndUnresolved()
    {
        var (exit, output, error) = await IngestAsync(Mixed);

        Assert.True(exit == 0, error);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(".spectra/generation-plan/storage.json", result.GetProperty("plan").GetString());
        Assert.True(File.Exists(PlanPath()));
        Assert.Equal(9, result.GetProperty("recommended").GetInt32());
        Assert.Equal(9, result.GetProperty("planned_total").GetInt32());
        Assert.Equal("proportional", result.GetProperty("count_rule").GetString());

        var batches = result.GetProperty("batches").EnumerateArray().ToList();
        Assert.Equal(["1", "2", "unsourced"], batches.Select(b => b.GetProperty("id").GetString()));
        Assert.Equal([2, 4, 3], batches.Select(b => b.GetProperty("count").GetInt32()));
        Assert.Equal(["docs/sharing.md#links"], GitWorkspace.Strings(batches[0].GetProperty("source_refs")));
        Assert.Equal(["docs/storage.md", "docs/storage.md#upload-quota", "docs/storage.md#limits"],
            GitWorkspace.Strings(batches[1].GetProperty("source_refs")));
        Assert.Equal(["Share links expire", "Upload | download"], GitWorkspace.Strings(batches[0].GetProperty("behaviors")));

        var unresolved = result.GetProperty("unresolved").EnumerateArray().ToList();
        Assert.Equal(["B5", "B6", "B7"], unresolved.Select(u => u.GetProperty("id").GetString()));
        Assert.Equal(["not found", "anchor not found", "empty ref"], unresolved.Select(u => u.GetProperty("reason").GetString()));
    }

    [Fact]
    public async Task TwoSpellingsOfOneSection_AreOneCanonicalRef_WithItsFingerprint()
    {
        await IngestAsync(Mixed);

        var plan = BaselineTestWorkspace.Json(File.ReadAllText(PlanPath()));
        var behaviors = plan.GetProperty("behaviors").EnumerateArray().ToList();
        Assert.Equal("docs/storage.md#upload-quota", behaviors[0].GetProperty("ref").GetString());
        Assert.Equal("docs/storage.md#upload-quota", behaviors[1].GetProperty("ref").GetString());
        Assert.Equal("docs/storage.md#Upload quota", behaviors[0].GetProperty("source").GetString());

        var raw = SectionExtractor.Extract(StorageDoc, "upload-quota").RawText!;
        Assert.Equal(SourceFingerprint.OfSection(raw), plan.GetProperty("sections").GetProperty("docs/storage.md#upload-quota").GetString());
        Assert.Equal(SourceFingerprint.OfSection(StorageDoc), plan.GetProperty("sections").GetProperty("docs/storage.md").GetString());
    }

    [Fact]
    public async Task AnAmbiguousAnchor_ResolvesToTheFirstHeading_WithOneNote()
    {
        await IngestAsync(Mixed);

        var plan = BaselineTestWorkspace.Json(File.ReadAllText(PlanPath()));
        var raw = SectionExtractor.Extract(StorageDoc, "limits").RawText!;
        Assert.StartsWith("## Limits\n\nFirst limits.", raw);
        Assert.Equal(SourceFingerprint.OfSection(raw), plan.GetProperty("sections").GetProperty("docs/storage.md#limits").GetString());
        var note = Assert.Single(GitWorkspace.Strings(plan.GetProperty("notes")));
        Assert.Contains("limits", note);
    }

    [Fact]
    public async Task TheSummary_ListsBatchAndUnresolvedLines_OnStderrInJsonMode()
    {
        var (_, _, error) = await IngestAsync(Mixed);

        Assert.Contains("batch 1 | count 2 | docs/sharing.md#links | Share links expire; Upload / download", error);
        Assert.Contains("batch unsourced | count 3 |  | Audit log; Missing section; No source", error);
        Assert.Contains("unresolved: B5 Audit log — not found", error);
    }

    [Fact]
    public async Task TheSameAnalysis_GivesTheSamePlanBytes_AndOtherSuitesAreUntouched()
    {
        await IngestAsync(Mixed);
        var first = File.ReadAllBytes(PlanPath());
        await IngestAsync(AnalysisJson(("Share links expire", "docs/sharing.md#links")), suite: "sharing");
        await IngestAsync(Mixed);

        Assert.Equal(first, File.ReadAllBytes(PlanPath()));
        Assert.True(File.Exists(PlanPath("sharing")));
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData("{\"nothing\": true}", 6)]
    public async Task AFailedAnalysis_WritesNoPlan_AndKeepsAnEarlierOne(string analysis, int expectedExit)
    {
        await IngestAsync(Mixed);
        var before = File.ReadAllBytes(PlanPath());

        var (exit, _, _) = await IngestAsync(analysis);

        Assert.Equal(expectedExit, exit);
        Assert.Equal(before, File.ReadAllBytes(PlanPath()));
    }

    [Fact]
    public async Task TheExistingFields_KeepTheirMeaning_DocumentsAnalyzedCountsDocuments()
    {
        var (_, output, _) = await IngestAsync(Mixed);

        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal(0, result.GetProperty("already_covered").GetInt32());
        Assert.Equal(9, result.GetProperty("breakdown").GetProperty("happy_path").GetInt32());
        Assert.Equal(9, result.GetProperty("technique_breakdown").GetProperty("EP").GetInt32());
        Assert.Equal(0, result.GetProperty("boundary_gaps").GetArrayLength());
        // docs/storage.md, docs/sharing.md, docs/nope.md — sections of one document count once.
        Assert.Equal(3, result.GetProperty("documents_analyzed").GetInt32());
    }
}
