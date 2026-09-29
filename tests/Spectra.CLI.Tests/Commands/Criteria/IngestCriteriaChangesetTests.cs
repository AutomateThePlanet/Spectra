using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 079 US3 (FR-012, FR-012a–c): every ingest reports its changeset — in the JSON result, and as a human summary
/// on stderr in JSON mode (stdout is exactly the JSON) or stdout in human mode. Failures carry no summary.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestCriteriaChangesetTests : IDisposable
{
    private const string Doc = "docs/checkout.md";
    private readonly CriteriaTestWorkspace _ws = new();

    public IngestCriteriaChangesetTests()
    {
        _ws.WriteDoc(Doc, "# Checkout\n");
        _ws.WriteTestIndex("checkout", ("TC-101", ["AC-CHECKOUT-003"]), ("TC-140", ["AC-CHECKOUT-003", "AC-CHECKOUT-001"]));
    }

    public void Dispose() => _ws.Dispose();

    private static object Item(string text, string? id = null, string priority = "medium") => id is null
        ? new { text, rfc2119 = "MUST", priority }
        : new { id, text, rfc2119 = "MUST", priority };

    private Task SeedAsync() => _ws.IngestAsync(Doc,
        Item("System MUST validate the card"), Item("System MUST show an error"), Item("System MUST retry once"));

    private Task<(int Exit, string Out, string Err)> ChangeAsync(params string[] extraArgs)
    {
        _ws.Response(
            Item("System MUST reject expired cards", id: "AC-CHECKOUT-001"),   // modified
            Item("System MUST show an error", priority: "high"),              // metadata
            Item("System MUST log the decline"));                             // added; 003 removed
        return _ws.RunAsync(["ai", "ingest-criteria", "--doc", Doc, "--from", ".spectra/criteria.json", .. extraArgs]);
    }

    [Fact]
    public async Task JsonMode_StdoutIsTheChangesetJson_StderrHasTheSummary()
    {
        await SeedAsync();

        var (exit, output, err) = await ChangeAsync("--output-format", "json");

        Assert.Equal(0, exit);
        var cs = CriteriaTestWorkspace.Json(output).GetProperty("changeset"); // stdout parses as one JSON document
        Assert.Equal("AC-CHECKOUT-004", cs.GetProperty("added")[0].GetProperty("id").GetString());
        var modified = cs.GetProperty("modified")[0];
        Assert.Equal(("AC-CHECKOUT-001", "System MUST validate the card", "System MUST reject expired cards"),
            (modified.GetProperty("id").GetString(), modified.GetProperty("old_text").GetString(), modified.GetProperty("new_text").GetString()));
        var removed = cs.GetProperty("removed")[0];
        Assert.Equal("AC-CHECKOUT-003", removed.GetProperty("id").GetString());
        Assert.Equal(2, removed.GetProperty("referencing_tests").GetInt32());
        Assert.Equal(["TC-101", "TC-140"], removed.GetProperty("test_ids").EnumerateArray().Select(e => e.GetString()));
        var metadata = cs.GetProperty("metadata")[0];
        Assert.Equal(("AC-CHECKOUT-002", "priority", "medium", "high"),
            (metadata.GetProperty("id").GetString(), metadata.GetProperty("field").GetString(),
             metadata.GetProperty("old").GetString(), metadata.GetProperty("new").GetString()));
        Assert.False(cs.GetProperty("is_empty").GetBoolean());

        Assert.Contains("+ AC-CHECKOUT-004", err);
        Assert.Contains("~ AC-CHECKOUT-001", err);
        Assert.Contains("- AC-CHECKOUT-003", err);
        Assert.Contains("still referenced by 2 tests: TC-101, TC-140", err);
        Assert.Contains("* AC-CHECKOUT-002  priority: medium → high", err);
    }

    [Fact]
    public async Task HumanMode_PrintsTheSummaryToStdout()
    {
        await SeedAsync();

        var (exit, output, _) = await ChangeAsync();

        Assert.Equal(0, exit);
        Assert.Contains("Criteria changeset for docs/checkout.md", output);
        Assert.Contains("- AC-CHECKOUT-003", output);
    }

    [Fact]
    public async Task ReIngestingTheSameCriteria_ReportsAnEmptyChangeset()
    {
        await SeedAsync();

        var (_, output, err) = await SeedAsyncAgain();

        Assert.True(CriteriaTestWorkspace.Json(output).GetProperty("changeset").GetProperty("is_empty").GetBoolean());
        Assert.Contains("No changes.", err);
    }

    private Task<(int Exit, string Out, string Err)> SeedAsyncAgain() => _ws.IngestAsync(Doc,
        Item("System MUST validate the card"), Item("System MUST show an error"), Item("System MUST retry once"));

    [Fact]
    public async Task DryRun_ReportsTheChangeset_AndWritesNothing()
    {
        await SeedAsync();
        var before = _ws.Snapshot();

        var (exit, output, err) = await ChangeAsync("--dry-run", "--output-format", "json");

        Assert.Equal(0, exit);
        var result = CriteriaTestWorkspace.Json(output);
        Assert.False(result.GetProperty("written").GetBoolean());
        Assert.Equal(1, result.GetProperty("changeset").GetProperty("removed").GetArrayLength());
        Assert.Contains("dry run", err);
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task Failure_PrintsOnlyTheFailureJson()
    {
        await SeedAsync();
        _ws.Response(Item("x", id: "AC-LOGIN-001"));

        var (exit, output, err) = await _ws.RunAsync(
            "ai", "ingest-criteria", "--doc", Doc, "--from", ".spectra/criteria.json", "--output-format", "json");

        Assert.Equal(7, exit);
        Assert.Equal(string.Empty, output.Trim());
        Assert.False(CriteriaTestWorkspace.Json(err).GetProperty("success").GetBoolean()); // the whole of stderr is JSON
        Assert.DoesNotContain("Criteria changeset", err);
    }
}
