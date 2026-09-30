using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 079 US4 (FR-015–FR-017): per-doc criteria files, normalised <c>source_doc</c>, and automatic migration
/// before an ingest writes.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestCriteriaStorageTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static object Item(string text) => new { text, rfc2119 = "MUST" };

    [Fact]
    public async Task SameFileNameInDifferentFolders_KeepsSeparateCriteria()
    {
        _ws.WriteDoc("docs/a/login.md", "# A\n");
        _ws.WriteDoc("docs/b/login.md", "# B\n");

        await _ws.IngestAsync("docs/a/login.md", Item("A requirement"));
        await _ws.IngestAsync("docs/b/login.md", Item("B requirement"));

        Assert.Equal("A requirement", (await _ws.ReadCriteriaAsync("docs/a/login.criteria.yaml")).Single().Text);
        Assert.Equal("B requirement", (await _ws.ReadCriteriaAsync("docs/b/login.criteria.yaml")).Single().Text);
    }

    [Fact]
    public async Task BackslashDocPath_IsStoredNormalised_AndDocsChangedSeesItUnchanged()
    {
        _ws.WriteDoc("docs/checkout.md", "# Checkout\n");

        var (exit, _, err) = await _ws.IngestAsync("docs\\checkout.md", Item("System MUST validate"));
        Assert.True(exit == 0, err);

        Assert.Equal("docs/checkout.md", (await _ws.ReadIndexAsync()).Sources.Single().SourceDoc);
        var output = new StringWriter();
        await new DocsChangedHandler(OutputFormat.Json, includeUnchanged: true, includeArchived: false)
            .ExecuteAsync(_ws.Root, output);
        var entry = CriteriaTestWorkspace.Json(output.ToString()).GetProperty("changed").EnumerateArray()
            .Single(e => e.GetProperty("path").GetString() == "docs/checkout.md");
        Assert.Equal("unchanged", entry.GetProperty("status").GetString());
    }

    [Fact]
    public async Task OldLayout_IsMigratedBeforeTheIngestWrites()
    {
        OldLayoutCriteria.Seed(_ws);

        var (exit, output, err) = await _ws.IngestAsync("docs/checkout.md",
            new { id = "AC-CHECKOUT-001", text = "System MUST validate the card", rfc2119 = "MUST" },
            Item("System MUST log declines"));

        Assert.True(exit == 0, err);
        var result = CriteriaTestWorkspace.Json(output);
        Assert.Equal(JsonValueKindObject, result.GetProperty("migration").ValueKind);
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/checkout.criteria.yaml")));
        Assert.Equal(["AC-CHECKOUT-001", "AC-CHECKOUT-008"], // 007 was the highest ever issued
            (await _ws.ReadCriteriaAsync("docs/checkout.criteria.yaml")).Select(c => c.Id));
        Assert.Contains("Migrated criteria storage first", err);
    }

    [Fact]
    public async Task DerivedPathOwnedByAnotherSource_Exits1_Collision()
    {
        // A repo-root folder named `imported/` maps onto the import directory.
        _ws.WriteDoc("imported/jira.md", "# oops\n");
        _ws.WriteCriteriaFile("imported/jira.criteria.yaml", "criteria:\n- id: AC-JIRA-001\n  text: imported\n");
        _ws.WriteIndexYaml("""
            version: 1
            sources:
            - file: imported/jira.criteria.yaml
              source_doc: jira.csv
              source_type: import
              criteria_count: 1
            """);

        var (exit, _, err) = await _ws.IngestAsync("imported/jira.md", Item("x"));

        Assert.Equal(1, exit);
        Assert.Equal("CRITERIA_FILE_COLLISION", CriteriaTestWorkspace.Json(err).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task PastCollision_ReExtractingEachDoc_SplitsThemIntoTheirOwnFiles()
    {
        OldLayoutCriteria.SeedCollision(_ws);

        var (exitA, _, errA) = await _ws.IngestAsync("docs/a/login.md", Item("A requirement"));
        var (exitB, _, errB) = await _ws.IngestAsync("docs/b/login.md",
            new { id = "AC-LOGIN-001", text = "B wrote last", rfc2119 = "MUST" });

        Assert.True(exitA == 0, errA);
        Assert.True(exitB == 0, errB);
        Assert.Equal("AC-LOGIN-002", (await _ws.ReadCriteriaAsync("docs/a/login.criteria.yaml")).Single().Id);
        Assert.Equal("AC-LOGIN-001", (await _ws.ReadCriteriaAsync("docs/b/login.criteria.yaml")).Single().Id); // kept by provenance
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/login.criteria.yaml"))); // no longer referenced → removed
    }

    private const System.Text.Json.JsonValueKind JsonValueKindObject = System.Text.Json.JsonValueKind.Object;
}
