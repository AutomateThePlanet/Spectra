using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GenerationPlanWorkspace;

namespace Spectra.CLI.Tests.Config;

/// <summary>
/// Spec 094 T7 (FR-006; 083 rule): <c>generation.batch_size</c> is optional — default 8, valid 1–50, <c>CONFIG_INVALID</c>
/// outside — checked against the real init template and a config without the key or with <c>"generation": null</c>. On
/// 55b541f the key didn't exist and the analysis ingest did not load the config through the checked loader.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GenerationBatchSizeConfigTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public GenerationBatchSizeConfigTests() => SetUpAsync(_ws, criteria: false).GetAwaiter().GetResult();

    public void Dispose() => _ws.Dispose();

    private static string OneBehaviour => AnalysisJson(("Quota refused", "docs/storage.md#upload-quota"));

    private void EditConfig(Action<JsonObject> edit)
    {
        var path = _ws.PathOf("spectra.config.json");
        var config = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(config);
        File.WriteAllText(path, config.ToJsonString());
    }

    private int PlanBatchSize() =>
        BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf(".spectra/generation-plan/storage.json"))).GetProperty("batch_size").GetInt32();

    [Fact]
    public async Task TheRealInitTemplate_HasNoKey_AndGetsEight_WithoutAWarning()
    {
        var generation = JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!["generation"]!.AsObject();
        Assert.False(generation.ContainsKey("batch_size"));

        var (exit, _, error) = await _ws.IngestAnalysisAsync(OneBehaviour);

        Assert.True(exit == 0, error);
        Assert.Equal(8, PlanBatchSize());
        Assert.DoesNotContain("Warning:", error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task TheRangeEnds_AreAccepted(int value)
    {
        EditConfig(c => c["generation"]!.AsObject()["batch_size"] = value);

        var (exit, _, error) = await _ws.IngestAnalysisAsync(OneBehaviour);

        Assert.True(exit == 0, error);
        Assert.Equal(value, PlanBatchSize());
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("51", true)]
    [InlineData("\"x\"", false)]
    public async Task AValueOutsideTheRange_IsConfigInvalid(string value, bool namesTheRange)
    {
        EditConfig(c => c["generation"]!.AsObject()["batch_size"] = JsonNode.Parse(value));

        var (exit, _, error) = await _ws.IngestAnalysisAsync(OneBehaviour);

        Assert.Equal(1, exit);
        var failure = BaselineTestWorkspace.Json(error.Trim().Split('\n').Last());
        Assert.Equal("CONFIG_INVALID", failure.GetProperty("error_code").GetString());
        if (namesTheRange)
        {
            Assert.Contains("generation.batch_size must be an integer from 1 to 50", failure.GetProperty("errors")[0].GetString());
        }

        Assert.False(File.Exists(_ws.PathOf(".spectra/generation-plan/storage.json")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AMissingOrNullGenerationSection_GetsTheDefaults(bool asNull)
    {
        EditConfig(c =>
        {
            if (asNull)
            {
                c["generation"] = null;
            }
            else
            {
                c.Remove("generation");
            }
        });

        var (exit, _, error) = await _ws.IngestAnalysisAsync(OneBehaviour);

        Assert.True(exit == 0, error);
        Assert.Equal(8, PlanBatchSize());
    }
}
