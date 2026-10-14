using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Tests.Commands;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 098 F9 (FR-019–FR-021, SC-009): the per-behaviour coverage rule reads <c>generation.coverage_threshold</c>
/// (default 0.6); <c>duplicate_threshold</c> is duplicate detection only. A workspace that tuned only
/// <c>duplicate_threshold</c> (≠ 0.6) keeps its coverage behaviour for one release, with a notice. The init template's
/// <c>duplicate_threshold: 0.6</c> gives no notice. On 986da01 coverage read <c>duplicate_threshold</c> and there was no
/// <c>coverage_threshold</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CoverageThresholdTests : IDisposable
{
    private const string Notice = "generation.coverage_threshold is not set; using generation.duplicate_threshold";

    private static readonly string Analysis = AnalysisJson(("Upload above the quota is refused", "docs/storage.md#upload-quota"));

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public CoverageThresholdTests()
    {
        SetUpAsync(_ws).GetAwaiter().GetResult();
        _ws.WriteTest("storage", "TC-105", ["docs/storage.md"], title: "Upload above quota is refused now");
    }

    public void Dispose() => _ws.Dispose();

    private void Generation(Action<JsonObject> edit) => _ws.EditConfig(c => edit(c["generation"]!.AsObject()));

    private async Task<(int Covered, string Err)> IngestAsync()
    {
        var (exit, output, error) = await _ws.IngestAnalysisAsync(Analysis);
        Assert.True(exit == 0, error);
        return (BaselineTestWorkspace.Json(output).GetProperty("already_covered").GetInt32(), error);
    }

    [Fact]
    public void TheInitTemplate_WritesCoverageThreshold()
    {
        var template = JsonNode.Parse(ConfigRuleMatrixTests.InitTemplate())!;

        Assert.Equal(0.6, template["generation"]!["coverage_threshold"]!.GetValue<double>());
        Assert.Equal(0.6, JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!["generation"]!["coverage_threshold"]!.GetValue<double>());
    }

    [Fact]
    public async Task CoverageThreshold_DecidesCoverage_AndDuplicateThresholdDoesNot()
    {
        Generation(g => { g["coverage_threshold"] = 0.9; g["duplicate_threshold"] = 0.1; });

        var (covered, error) = await IngestAsync();

        Assert.Equal(0, covered);
        Assert.DoesNotContain(Notice, error);
    }

    [Fact]
    public async Task OnlyATunedDuplicateThreshold_IsUsedForCoverage_WithANotice()
    {
        Generation(g => { g.Remove("coverage_threshold"); g["duplicate_threshold"] = 0.9; });

        var (covered, error) = await IngestAsync();

        Assert.Equal(0, covered);
        Assert.Equal(1, UpdatePromptWorkspace.Count(error, Notice));
        Assert.Contains("(0.9)", error);
    }

    [Fact]
    public async Task ThePre098Template_DuplicateThresholdAtTheDefault_GivesNoNotice()
    {
        Generation(g => g.Remove("coverage_threshold"));

        var (covered, error) = await IngestAsync();

        Assert.Equal(1, covered);
        Assert.DoesNotContain(Notice, error);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-0.1)]
    public async Task ACoverageThresholdOutsideZeroToOne_IsConfigInvalid(double value)
    {
        Generation(g => g["coverage_threshold"] = value);

        var (exit, _, error) = await _ws.IngestAnalysisAsync(Analysis);

        Assert.NotEqual(0, exit);
        Assert.Contains("generation.coverage_threshold", error);
    }

    [Fact]
    public async Task ThePlan_RecordsTheCoverageThreshold()
    {
        Generation(g => g["coverage_threshold"] = 0.75);

        await IngestAsync();

        var plan = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf(".spectra/generation-plan/storage.json")));
        Assert.Equal(0.75, plan.GetProperty("coverage_threshold").GetDouble());
    }

    [Fact]
    public async Task A095Plan_WithDuplicateThreshold_StillCountsAsKnownCoverage()
    {
        await IngestAsync();
        var path = _ws.PathOf(".spectra/generation-plan/storage.json");
        var plan = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var threshold = plan["coverage_threshold"]!.GetValue<double>();
        plan.Remove("coverage_threshold");
        plan["duplicate_threshold"] = threshold;
        File.WriteAllText(path, plan.ToJsonString());

        var (exit, _, error) = await _ws.CompilePromptAsync("--suite", "storage", "--batch", "unsourced");

        Assert.DoesNotContain("predates per-behaviour coverage", error);
        _ = exit;
    }
}
