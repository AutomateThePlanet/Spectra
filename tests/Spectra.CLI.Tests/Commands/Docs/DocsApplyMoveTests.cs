using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Docs;

/// <summary>Spec 079 FR-020: <c>spectra docs apply-move</c> keeps criteria and IDs when an unchanged doc moves.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class DocsApplyMoveTests : IDisposable
{
    private const string Content = "# Refunds\n\nRefunds MUST be issued within 5 days.\n";
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private async Task SeedAndMoveAsync(string to = "docs/billing/refunds.md")
    {
        _ws.WriteDoc("docs/refunds.md", Content);
        var (exit, _, err) = await _ws.IngestAsync("docs/refunds.md",
            new { text = "Refunds MUST be issued within 5 days", rfc2119 = "MUST" },
            new { text = "Refunds SHOULD notify", rfc2119 = "SHOULD" });
        Assert.True(exit == 0, err);
        Directory.CreateDirectory(Path.GetDirectoryName(_ws.PathOf(to))!);
        File.Move(_ws.PathOf("docs/refunds.md"), _ws.PathOf(to));
    }

    [Fact]
    public async Task ValidMove_KeepsIdsAndTexts_RepointsEverything()
    {
        await SeedAndMoveAsync();
        var before = await _ws.ReadCriteriaAsync("docs/refunds.criteria.yaml");

        var (exit, output, err) = await _ws.RunAsync(
            "docs", "apply-move", "--from", "docs/refunds.md", "--to", "docs/billing/refunds.md", "--output-format", "json");

        Assert.True(exit == 0, err);
        Assert.Equal(2, CriteriaTestWorkspace.Json(output).GetProperty("criteria_count").GetInt32());
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/docs/refunds.criteria.yaml")));
        var after = await _ws.ReadCriteriaAsync("docs/billing/refunds.criteria.yaml");
        Assert.Equal(before.Select(c => (c.Id, c.Text)), after.Select(c => (c.Id, c.Text)));
        Assert.All(after, c => Assert.Equal("docs/billing/refunds.md", c.SourceDoc));
        Assert.Contains("# Extracted from: docs/billing/refunds.md",
            File.ReadAllText(_ws.PathOf("docs/criteria/docs/billing/refunds.criteria.yaml")));

        var source = (await _ws.ReadIndexAsync()).Sources.Single();
        Assert.Equal(("docs/billing/refunds.md", "docs/billing/refunds.criteria.yaml"), (source.SourceDoc, source.File));
    }

    [Fact]
    public async Task RenamedFile_KeepsOldIdPrefix_NewCriteriaUseTheNewComponent()
    {
        await SeedAndMoveAsync("docs/billing-refunds.md");
        await _ws.RunAsync("docs", "apply-move", "--from", "docs/refunds.md", "--to", "docs/billing-refunds.md");

        var (exit, output, err) = await _ws.IngestAsync("docs/billing-refunds.md",
            new { id = "AC-REFUNDS-001", text = "Refunds MUST be issued within 5 days", rfc2119 = "MUST" },
            new { text = "Refunds MUST be logged", rfc2119 = "MUST" });

        Assert.True(exit == 0, err);
        var ids = CriteriaTestWorkspace.Json(output).GetProperty("ids").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(["AC-REFUNDS-001", "AC-BILLING-REFUNDS-001"], ids);
    }

    [Fact]
    public async Task DryRun_WritesNothing()
    {
        await SeedAndMoveAsync();
        var before = _ws.Snapshot();

        var (exit, _, _) = await _ws.RunAsync(
            "docs", "apply-move", "--from", "docs/refunds.md", "--to", "docs/billing/refunds.md", "--dry-run");

        Assert.Equal(0, exit);
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task Refusals_Exit4_WithTheirCodes()
    {
        await SeedAndMoveAsync();

        async Task<string?> Code(params string[] args)
        {
            var (exit, _, err) = await _ws.RunAsync(["docs", "apply-move", .. args, "--output-format", "json"]);
            Assert.Equal(4, exit);
            return CriteriaTestWorkspace.Json(err).GetProperty("error_code").GetString();
        }

        _ws.WriteDoc("docs/refunds.md", Content); // old path back on disk
        Assert.Equal("DOC_STILL_EXISTS", await Code("--from", "docs/refunds.md", "--to", "docs/billing/refunds.md"));
        File.Delete(_ws.PathOf("docs/refunds.md"));

        Assert.Equal("TARGET_MISSING", await Code("--from", "docs/refunds.md", "--to", "docs/nowhere.md"));
        Assert.Equal("NOT_INDEXED", await Code("--from", "docs/never.md", "--to", "docs/billing/refunds.md"));

        _ws.WriteDoc("docs/billing/refunds.md", Content + "Changed.\n");
        Assert.Equal("CONTENT_CHANGED", await Code("--from", "docs/refunds.md", "--to", "docs/billing/refunds.md"));
    }

    [Fact]
    public async Task TargetAlreadyIndexed_IsRefused()
    {
        await SeedAndMoveAsync();
        await _ws.IngestAsync("docs/billing/refunds.md", new { text = "Other MUST hold", rfc2119 = "MUST" });

        var (exit, _, err) = await _ws.RunAsync(
            "docs", "apply-move", "--from", "docs/refunds.md", "--to", "docs/billing/refunds.md", "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Equal("TARGET_INDEXED", CriteriaTestWorkspace.Json(err).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task OldLayout_IsMigratedBeforeTheMove()
    {
        OldLayoutCriteria.Seed(_ws);
        // login.md's recorded hash in the seed is "bbb"; make the moved file's hash match it.
        var index = File.ReadAllText(_ws.IndexPath);
        Directory.CreateDirectory(_ws.PathOf("docs/auth"));
        File.Move(_ws.PathOf("docs/login.md"), _ws.PathOf("docs/auth/login.md"));
        var hash = Spectra.CLI.Infrastructure.FileHasher.ComputeHash(File.ReadAllText(_ws.PathOf("docs/auth/login.md")));
        _ws.WriteIndexYaml(index.Replace("doc_hash: bbb", $"doc_hash: {hash}"));

        var (exit, output, err) = await _ws.RunAsync(
            "docs", "apply-move", "--from", "docs/login.md", "--to", "docs/auth/login.md", "--output-format", "json");

        Assert.True(exit == 0, err);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, CriteriaTestWorkspace.Json(output).GetProperty("migration").ValueKind);
        Assert.Equal("AC-LOGIN-002", (await _ws.ReadCriteriaAsync("docs/auth/login.criteria.yaml")).Single().Id);
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/login.criteria.yaml")));
    }
}
