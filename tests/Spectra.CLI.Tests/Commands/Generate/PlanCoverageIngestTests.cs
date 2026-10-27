using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 095 T3 (FR-001–FR-007, FR-011): the analysis ingest decides per behaviour whether an existing test of the suite
/// covers it (title similarity ≥ <c>generation.duplicate_threshold</c>), counts <c>already_covered</c> /
/// <c>recommended</c> from that, lists the covered ones, and batches only the uncovered ones. On db20415
/// <c>already_covered</c> was the suite's test count and every behaviour was batched.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class PlanCoverageIngestTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public PlanCoverageIngestTests() => SetUpAsync(_ws).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    /// <summary>TC-101..TC-103 match B1, B3, B4; TC-104 matches nothing.</summary>
    private void WriteSuiteTests()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#upload-quota"], title: "Quota refused above 30 GB");
        _ws.WriteTest("storage", "TC-102", ["docs/sharing.md#links"], title: "Links expire after 7 days");
        _ws.WriteTest("storage", "TC-103", ["docs/storage.md"], title: "Audit log kept 90 days");
        _ws.WriteTest("storage", "TC-104", ["docs/storage.md"], title: "Unrelated archive export to CSV");
    }

    /// <summary>B1 covered, B2 open, B3 covered, B4 covered + unresolvable source, B5 open, B6 open + unresolvable.</summary>
    private static string Mixed => AnalysisJson(
        ("Quota refused above 30 GB", "docs/storage.md#upload-quota"),
        ("Quota shown in settings", "docs/storage.md#quota-settings"),
        ("Links expire after 7 days", "docs/sharing.md#links"),
        ("Audit log kept 90 days", "docs/nope.md"),
        ("Limits apply to every upload", "docs/storage.md#limits"),
        ("Restore keeps the sharing settings", "docs/nope2.md"));

    private async Task<(JsonElement Result, string Err)> IngestAsync(string analysis)
    {
        var (exit, output, error) = await _ws.IngestAnalysisAsync(analysis);
        Assert.True(exit == 0, error);
        return (BaselineTestWorkspace.Json(output), error);
    }

    private void EditConfig(Action<JsonObject> edit)
    {
        var path = _ws.PathOf("spectra.config.json");
        var config = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(config);
        File.WriteAllText(path, config.ToJsonString());
    }

    private static string[] Ids(JsonElement array) => array.EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToArray();

    [Fact]
    public async Task TheCounts_ComeFromThePerBehaviourRule_AndTheCoveredAreListed()
    {
        WriteSuiteTests();

        var (result, error) = await IngestAsync(Mixed);

        Assert.Equal(3, result.GetProperty("already_covered").GetInt32());
        Assert.Equal(3, result.GetProperty("recommended").GetInt32());
        var covered = result.GetProperty("covered").EnumerateArray().ToList();
        Assert.Equal(["B1", "B3", "B4"], covered.Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(["TC-101", "TC-102", "TC-103"], covered.Select(c => c.GetProperty("covered_by").GetString()));
        Assert.Equal("Quota refused above 30 GB", covered[0].GetProperty("title").GetString());

        var lines = error.Replace("\r\n", "\n").Split('\n');
        var lastBatch = Array.FindLastIndex(lines, l => l.StartsWith("batch ", StringComparison.Ordinal));
        var firstCovered = Array.FindIndex(lines, l => l.StartsWith("covered: ", StringComparison.Ordinal));
        var firstUnresolved = Array.FindIndex(lines, l => l.StartsWith("unresolved: ", StringComparison.Ordinal));
        Assert.Contains("covered: Quota refused above 30 GB (TC-101)", lines);
        Assert.True(lastBatch < firstCovered && firstCovered < firstUnresolved, error);
    }

    [Fact]
    public async Task ACoveredBehaviour_WithAnUnresolvableSource_IsOnlyUnderCovered()
    {
        WriteSuiteTests();

        var (result, error) = await IngestAsync(Mixed);

        Assert.Contains("B4", Ids(result.GetProperty("covered")));
        Assert.Equal(["B6"], Ids(result.GetProperty("unresolved")));
        Assert.DoesNotContain(result.GetProperty("batches").EnumerateArray(),
            b => b.GetProperty("behaviors").EnumerateArray().Any(t => t.GetString() == "Audit log kept 90 days"));
        Assert.Single(error.Replace("\r\n", "\n").Split('\n'), l => l.Contains("Audit log kept 90 days", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThePlan_BatchesOnlyUncoveredBehaviours_CountsAddUpToRecommended()
    {
        WriteSuiteTests();

        var (result, _) = await IngestAsync(Mixed);

        var batches = result.GetProperty("batches").EnumerateArray().ToList();
        Assert.Equal(["1", "unsourced"], batches.Select(b => b.GetProperty("id").GetString()));
        Assert.Equal(["Quota shown in settings", "Limits apply to every upload"], GitWorkspace.Strings(batches[0].GetProperty("behaviors")));
        Assert.Equal(["Restore keeps the sharing settings"], GitWorkspace.Strings(batches[1].GetProperty("behaviors")));
        Assert.Equal([2, 1], batches.Select(b => b.GetProperty("count").GetInt32()));
        Assert.Equal(3, result.GetProperty("planned_total").GetInt32());

        var plan = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf(".spectra/generation-plan/storage.json")));
        Assert.Equal("TC-101", plan.GetProperty("behaviors")[0].GetProperty("covered_by").GetString());
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("behaviors")[1].GetProperty("covered_by").ValueKind);
    }

    [Fact]
    public async Task TheCounts_AreTheSame_WithAndWithoutCriteria()
    {
        WriteSuiteTests();
        var (withCriteria, _) = await IngestAsync(Mixed);
        foreach (var file in Directory.GetFiles(_ws.PathOf("docs/criteria"), "*.criteria.yaml", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        var (withoutCriteria, _) = await IngestAsync(Mixed);

        foreach (var result in new[] { withCriteria, withoutCriteria })
        {
            Assert.Equal(3, result.GetProperty("already_covered").GetInt32());
            Assert.Equal(3, result.GetProperty("recommended").GetInt32());
        }
    }

    [Fact]
    public async Task TheThreshold_ComesFromTheConfig()
    {
        _ws.WriteTest("storage", "TC-105", ["docs/storage.md"], title: "Upload above quota is refused now");
        var analysis = AnalysisJson(("Upload above the quota is refused", "docs/storage.md#upload-quota"));

        var (defaultThreshold, error) = await IngestAsync(analysis);
        // Spec 098 F9 (deliberate change): coverage reads generation.coverage_threshold (was duplicate_threshold).
        EditConfig(c => c["generation"]!.AsObject()["coverage_threshold"] = 0.9);
        var (strict, _) = await IngestAsync(analysis);

        Assert.Equal(1, defaultThreshold.GetProperty("already_covered").GetInt32());
        Assert.DoesNotContain("Warning:", error);
        Assert.Equal(0, strict.GetProperty("already_covered").GetInt32());
        Assert.Equal(1, strict.GetProperty("recommended").GetInt32());
    }

    [Fact]
    public async Task AThresholdOutsideZeroToOne_WarnsOnStderr_WithoutRefusing()
    {
        WriteSuiteTests();
        // Spec 098 F9 (deliberate change): a pre-098 workspace (no coverage_threshold) whose duplicate_threshold is tuned
        // keeps using it for coverage, so its out-of-range warning still names that key.
        EditConfig(c => { c["generation"]!.AsObject().Remove("coverage_threshold"); c["generation"]!.AsObject()["duplicate_threshold"] = 0; });

        var (result, error) = await IngestAsync(Mixed);

        Assert.Contains("generation.duplicate_threshold = 0 is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered.", error);
        Assert.Equal(0, result.GetProperty("recommended").GetInt32());
    }

    [Fact]
    public async Task AllCovered_GivesNoBatches()
    {
        WriteSuiteTests();

        var (result, _) = await IngestAsync(AnalysisJson(
            ("Quota refused above 30 GB", "docs/storage.md#upload-quota"),
            ("Links expire after 7 days", "docs/sharing.md#links")));

        Assert.Equal(0, result.GetProperty("recommended").GetInt32());
        Assert.Equal(0, result.GetProperty("batches").GetArrayLength());
        Assert.Equal(0, result.GetProperty("planned_total").GetInt32());
    }

    [Fact]
    public async Task FiftyTests_TwelveGaps_RecommendTwelve_AndPlanTwelve()
    {
        Directory.CreateDirectory(_ws.PathOf("test-cases/storage"));
        for (var i = 1; i <= 50; i++)
        {
            File.WriteAllText(_ws.PathOf($"test-cases/storage/TC-{i:D3}.md"),
                BaselineTestWorkspace.TestMarkdown($"TC-{i:D3}", ["docs/storage.md"], title: $"Existing behaviour number {i} of the archive module"));
        }

        _ws.RebuildIndex("storage");
        var behaviors = new List<(string, string)>
        {
            ("Existing behaviour number 7 of the archive module", "docs/storage.md#upload-quota"),
            ("Existing behaviour number 21 of the archive module", "docs/storage.md#limits"),
            ("Existing behaviour number 40 of the archive module", "docs/sharing.md#links"),
        };
        behaviors.AddRange(Enumerable.Range(1, 12).Select(i =>
            ($"Quota warning banner variant {(char)('A' + i)} turns red", i % 2 == 0 ? "docs/storage.md#upload-quota" : "docs/sharing.md#links")));

        var (result, _) = await IngestAsync(AnalysisJson([.. behaviors]));

        Assert.Equal(3, result.GetProperty("already_covered").GetInt32());
        Assert.Equal(12, result.GetProperty("recommended").GetInt32());
        Assert.Equal(12, result.GetProperty("batches").EnumerateArray().Sum(b => b.GetProperty("count").GetInt32()));
    }
}
