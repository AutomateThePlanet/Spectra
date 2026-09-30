using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Docs;

/// <summary>Spec 079 FR-021: <c>spectra docs retire</c> — explicit, reviewable retirement of a deleted doc.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class DocsRetireTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public DocsRetireTests()
    {
        _ws.WriteDoc("docs/old.md", "# Old\n");
        _ws.WriteDoc("docs/kept.md", "# Kept\n");
        _ws.WriteTestIndex("checkout", ("TC-101", ["AC-OLD-001"]));
        _ws.WriteTestIndex("login", ("TC-200", ["AC-OLD-001", "AC-OLD-002"]));
    }

    public void Dispose() => _ws.Dispose();

    private async Task SeedAsync()
    {
        await _ws.IngestAsync("docs/old.md",
            new { text = "Old MUST one", rfc2119 = "MUST" }, new { text = "Old MUST two", rfc2119 = "MUST" });
        await _ws.IngestAsync("docs/kept.md", new { text = "Kept MUST stay", rfc2119 = "MUST" });
        File.Delete(_ws.PathOf("docs/old.md"));
    }

    [Fact]
    public async Task DryRun_ReportsEveryCriterionRemoved_WithReferences_AndWritesNothing()
    {
        await SeedAsync();
        var before = _ws.Snapshot();

        var (exit, output, err) = await _ws.RunAsync("docs", "retire", "--doc", "docs/old.md", "--dry-run", "--output-format", "json");

        Assert.True(exit == 0, err);
        var result = CriteriaTestWorkspace.Json(output);
        Assert.False(result.GetProperty("written").GetBoolean());
        var removed = result.GetProperty("changeset").GetProperty("removed").EnumerateArray().ToList();
        Assert.Equal(["AC-OLD-001", "AC-OLD-002"], removed.Select(r => r.GetProperty("id").GetString()));
        Assert.Equal(2, removed[0].GetProperty("referencing_tests").GetInt32());
        Assert.Contains("still referenced by 2 tests", err); // human summary on stderr in JSON mode
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task Retire_RemovesEntryAndFile_KeepsMarks_AndDropsOutOfCoverage()
    {
        await SeedAsync();
        var marksBefore = (await _ws.ReadIndexAsync()).IdHighWaterMarks!;

        var (exit, _, err) = await _ws.RunAsync("docs", "retire", "--doc", "docs/old.md");

        Assert.True(exit == 0, err);
        var index = await _ws.ReadIndexAsync();
        Assert.Equal(["docs/kept.md"], index.Sources.Select(s => s.SourceDoc));
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/docs/old.criteria.yaml")));
        Assert.Equal(marksBefore, index.IdHighWaterMarks!);

        var output = new StringWriter();
        await new DocsChangedHandler(OutputFormat.Json, true, false).ExecuteAsync(_ws.Root, output);
        Assert.DoesNotContain("docs/old.md", output.ToString());

        var coverage = await new AcceptanceCriteriaCoverageAnalyzer()
            .AnalyzeFromDirectoryAsync(_ws.CriteriaDir, _ws.IndexPath, Array.Empty<TestCase>());
        Assert.Equal(1, coverage.TotalCriteria);
    }

    [Fact]
    public async Task ExistingDoc_IsRefused_Exit4()
    {
        await SeedAsync();

        var (exit, _, err) = await _ws.RunAsync("docs", "retire", "--doc", "docs/kept.md", "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Equal("DOC_STILL_EXISTS", CriteriaTestWorkspace.Json(err).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task UnknownDoc_IsRefused_Exit4()
    {
        var (exit, _, err) = await _ws.RunAsync("docs", "retire", "--doc", "docs/never.md", "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Equal("NOT_INDEXED", CriteriaTestWorkspace.Json(err).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task OldLayout_IsMigratedBeforeRetiring()
    {
        OldLayoutCriteria.Seed(_ws);
        File.Delete(_ws.PathOf("docs/login.md"));

        var (exit, output, err) = await _ws.RunAsync("docs", "retire", "--doc", "docs/login.md", "--output-format", "json");

        Assert.True(exit == 0, err);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, CriteriaTestWorkspace.Json(output).GetProperty("migration").ValueKind);
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/checkout.criteria.yaml")));
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/login.criteria.yaml")));
    }
}
