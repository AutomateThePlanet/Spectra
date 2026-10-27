using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 079 FR-008: <c>ai analyze --import-criteria</c> allocates from the same committed high-water marks as
/// extraction, merges before numbering, and so never collides with extracted IDs or consumes numbers on re-import.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ImportCriteriaAllocationTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public ImportCriteriaAllocationTests() => _ws.WriteDoc("docs/checkout.md", "# Checkout\n");

    public void Dispose() => _ws.Dispose();

    // `source` is the external key the merger matches on (e.g. a Jira issue), so each is distinct.
    private string WriteImport() => _ws.WriteDoc("jira-export.yaml", """
        criteria:
        - text: Refund MUST be issued within 5 days
          source: checkout
        - text: Refund SHOULD notify the customer
          source: PROJ 42
        """);

    [Fact]
    public async Task Import_AfterExtraction_ContinuesThePrefixesNumbering()
    {
        await _ws.IngestAsync("docs/checkout.md",
            new { text = "one", rfc2119 = "MUST" }, new { text = "two", rfc2119 = "MUST" });
        WriteImport();

        var (exit, _, err) = await _ws.RunAsync("ai", "analyze", "--import-criteria", "jira-export.yaml");

        Assert.True(exit == 0, err);
        var imported = await _ws.ReadCriteriaAsync("imported/jira-export.criteria.yaml");
        Assert.Equal(["AC-CHECKOUT-003", "AC-PROJ-42-001"], imported.Select(c => c.Id));
        Assert.Equal(3, (await _ws.ReadIndexAsync()).IdHighWaterMarks!["AC-CHECKOUT"]);
    }

    [Fact]
    public async Task ReImport_KeepsIds_AndIssuesNoNewNumbers()
    {
        WriteImport();
        await _ws.RunAsync("ai", "analyze", "--import-criteria", "jira-export.yaml");
        var first = await _ws.ReadCriteriaAsync("imported/jira-export.criteria.yaml");
        var marks = (await _ws.ReadIndexAsync()).IdHighWaterMarks!;

        var (exit, _, err) = await _ws.RunAsync("ai", "analyze", "--import-criteria", "jira-export.yaml");

        Assert.True(exit == 0, err);
        var second = await _ws.ReadCriteriaAsync("imported/jira-export.criteria.yaml");
        Assert.Equal(first.Select(c => c.Id), second.Select(c => c.Id));
        Assert.Equal(marks, (await _ws.ReadIndexAsync()).IdHighWaterMarks!);
    }

    [Fact]
    public async Task Import_OnOldLayout_MigratesFirst()
    {
        _ws.WriteCriteriaFile("checkout.criteria.yaml", "criteria:\n- id: AC-CHECKOUT-001\n  text: one\n  source_doc: docs/checkout.md\n");
        _ws.WriteIndexYaml("""
            version: 1
            sources:
            - file: checkout.criteria.yaml
              source_doc: docs/checkout.md
              source_type: document
              criteria_count: 1
            """);
        WriteImport();

        var (exit, _, err) = await _ws.RunAsync("ai", "analyze", "--import-criteria", "jira-export.yaml");

        Assert.True(exit == 0, err);
        Assert.Contains("Migrated criteria storage first", err);
        Assert.True(File.Exists(_ws.PathOf("docs/criteria/docs/checkout.criteria.yaml")));
        Assert.False(File.Exists(_ws.PathOf("docs/criteria/checkout.criteria.yaml")));
        Assert.Equal("AC-CHECKOUT-002", (await _ws.ReadCriteriaAsync("imported/jira-export.criteria.yaml"))[0].Id);
    }
}
