using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 097 F6 (FR-017, FR-018; SC-005): <c>--dry-run</c> on <c>ingest-grounding --all</c>, <c>ingest-verdict</c> and
/// <c>ingest-analysis</c> reports exactly what a real run does (the same JSON plus <c>dry_run: true</c>, the same exit
/// code) and leaves the workspace byte-identical. <c>ingest-grounding --test</c> refuses it. On 807047c the global
/// <c>--dry-run</c> was accepted and ignored: these commands wrote anyway.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BatchDryRunTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);
    private readonly List<BaselineTestWorkspace> _copies = [];

    public BatchDryRunTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.TwoSuites();
        foreach (var (id, verdict) in new[] { ("TC-101", "grounded"), ("TC-102", "grounded"), ("TC-103", "partial") })
        {
            var (code, _, err) = _ws.CompileCriticAsync("a", id).GetAwaiter().GetResult();
            Assert.True(code == 0, err);
            _ws.WriteVerdict(id, verdict);
        }
    }

    public void Dispose()
    {
        _ws.Dispose();
        foreach (var copy in _copies)
        {
            copy.Dispose();
        }
    }

    /// <summary>Runs the command with <c>--dry-run</c> here and without it on a copy; returns both results.</summary>
    private async Task<((int Exit, string Out, string Err) Dry, (int Exit, string Out, string Err) Real)> BothAsync(
        BaselineTestWorkspace ws, params string[] args)
    {
        var copy = ws.CopyWorkspace();
        _copies.Add(copy);
        var before = ws.Tree();

        var dry = await ws.RunAsync([.. args, "--dry-run", "--output-format", "json"]);
        var real = await copy.RunAsync([.. args, "--output-format", "json"]);

        Assert.Equal(before, ws.Tree());
        Assert.Equal(real.Exit, dry.Exit);
        Assert.True(VerdictWorkspace.Json(dry.Out).GetProperty("dry_run").GetBoolean());
        Assert.Equal(VerificationWorkspace.Comparable(real.Out), VerificationWorkspace.Comparable(dry.Out));
        return (dry, real);
    }

    [Fact]
    public async Task IngestGroundingAll_WritesNothing_AndReportsTheRealOutcome()
    {
        var (dry, real) = await BothAsync(_ws, "ai", "ingest-grounding", "--suite", "a", "--all");

        Assert.Equal(0, real.Exit);
        Assert.Equal(2, VerdictWorkspace.Json(dry.Out).GetProperty("written").GetInt32());
    }

    [Fact]
    public async Task IngestGroundingAll_WithIds_WritesNothing() =>
        await BothAsync(_ws, "ai", "ingest-grounding", "--suite", "a", "--all", "--ids", "TC-101");

    [Fact]
    public async Task IngestGroundingAllRepaired_WritesNothing() =>
        await BothAsync(_ws, "ai", "ingest-grounding", "--suite", "a", "--all", "--repaired", "--repair-attempts", "1", "--ids", "TC-103");

    [Fact]
    public async Task IngestVerdictAll_ReportsDryRun() =>
        await BothAsync(_ws, "ai", "ingest-verdict", "--suite", "a", "--all");

    [Fact]
    public async Task IngestVerdictFrom_ReportsDryRun() =>
        await BothAsync(_ws, "ai", "ingest-verdict", "--from", _ws.VerdictPath("TC-101"));

    [Fact]
    public async Task IngestAnalysis_WritesNoPlan()
    {
        using var ws = new BaselineTestWorkspace(writeConfig: false);
        await GenerationPlanWorkspace.SetUpAsync(ws);
        File.WriteAllText(ws.PathOf(".spectra/analysis.json"),
            GenerationPlanWorkspace.AnalysisJson(("Quota refused above 30 GB", "docs/storage.md#upload-quota")));

        var (dry, _) = await BothAsync(ws, "ai", "ingest-analysis", "--suite", "storage", "--from", ".spectra/analysis.json");

        Assert.Equal(0, dry.Exit);
        Assert.False(File.Exists(ws.PathOf(".spectra/generation-plan/storage.json")));
        Assert.Contains("Total behaviors", dry.Err); // the human summary, on stderr in JSON mode
    }

    [Fact]
    public async Task IngestGroundingTest_RefusesDryRun()
    {
        var before = _ws.Tree();

        var (exit, _, error) = await _ws.RunAsync("ai", "ingest-grounding", "--suite", "a", "--test", "TC-101", "--dry-run", "--output-format", "json");

        Assert.Equal(1, exit);
        Assert.Contains("--dry-run requires --all", error);
        Assert.Equal(before, _ws.Tree());
    }
}
