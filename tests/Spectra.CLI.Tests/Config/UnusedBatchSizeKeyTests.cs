using System.Text.Json.Nodes;
using Spectra.CLI.Tests.Commands;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Config;

namespace Spectra.CLI.Tests.Config;

/// <summary>
/// Spec 096 F9 (FR-013): the unused <c>ai.generation_batch_size</c> leaves the init template (<c>generation.batch_size</c>
/// is the one that applies, Spec 094), while a config that still contains it loads as before (083 rule). On 23aa08c the
/// init template still wrote it.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UnusedBatchSizeKeyTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void TheInitTemplate_HasNoGenerationBatchSize()
    {
        Assert.DoesNotContain("generation_batch_size", ConfigRuleMatrixTests.InitTemplate());
    }

    [Fact]
    public async Task AnOldConfigWithTheKey_StillLoads()
    {
        await _ws.InitWithRealTemplateAsync();
        var path = _ws.PathOf("spectra.config.json");
        var config = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        config["ai"]!.AsObject()["generation_batch_size"] = 30;
        File.WriteAllText(path, config.ToJsonString());

        var loaded = await ConfigLoader.LoadForWriteAsync(_ws.Root, ConfigSections.Source, CancellationToken.None);

        Assert.Null(loaded.Error);
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nText.\n");
        File.WriteAllText(_ws.PathOf(".spectra/analysis.json"),
            "{\"behaviors\":[{\"category\":\"happy_path\",\"title\":\"Quota\",\"source\":\"docs/storage.md\",\"technique\":\"EP\"}]}");
        var (exit, _, error) = await _ws.RunAsync("ai", "ingest-analysis", "--suite", "storage", "--from", ".spectra/analysis.json", "--output-format", "json");
        Assert.True(exit == 0, error);
    }
}
