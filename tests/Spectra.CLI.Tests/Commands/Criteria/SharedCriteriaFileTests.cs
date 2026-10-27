using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 080 B1–B3 (FR-006, FR-007, FR-007a, FR-008, SC-004): a criteria file shared by two documents (a pre-079
/// base-name collision) is handled by per-criterion <c>source_doc</c> provenance everywhere — compile, ingest,
/// apply-move and retire — and a document leaving the file takes its criteria out of it.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SharedCriteriaFileTests : IDisposable
{
    private const string Shared = "login.criteria.yaml";
    private const string DocA = "docs/a/login.md";
    private const string DocB = "docs/b/login.md";
    private const string BContent = "# Login B\n\nUsers MUST see B.\n";

    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    /// <summary>A's 001/002 and B's 003 in one shared file, optionally with an unowned (no source_doc) criterion.</summary>
    private async Task SeedAsync(bool withUnowned = true)
    {
        _ws.WriteDoc(DocA, "# Login A\n\nUsers MUST see A.\n");
        var bPath = _ws.WriteDoc(DocB, BContent);
        var bHash = await FileHasher.ComputeFileHashAsync(bPath);

        _ws.WriteCriteriaFile(Shared, $"""
            criteria:
            - id: AC-LOGIN-001
              text: A one MUST hold
              source_doc: {DocA}
            - id: AC-LOGIN-002
              text: A two MUST hold
              source_doc: {DocA}
            - id: AC-LOGIN-003
              text: B three MUST hold
              source_doc: {DocB}
            {(withUnowned ? "- id: AC-LOGIN-009\n  text: Legacy MUST hold\n" : "")}
            """);
        _ws.WriteIndexYaml($"""
            version: 1
            sources:
            - file: {Shared}
              source_doc: {DocA}
              source_type: document
              criteria_count: 2
            - file: {Shared}
              source_doc: {DocB}
              source_type: document
              doc_hash: {bHash}
              criteria_count: 1
            """);
    }

    private async Task<string[]> IdsInAsync(string file) =>
        (await _ws.ReadCriteriaAsync(file)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();

    /// <summary>Every criterion ID in every criteria file (what the recursive coverage scanners see).</summary>
    private async Task<string[]> AllIdsOnDiskAsync()
    {
        var ids = new List<string>();
        foreach (var file in Directory.GetFiles(_ws.CriteriaDir, "*.criteria.yaml", SearchOption.AllDirectories))
        {
            ids.AddRange((await _ws.ReadCriteriaAsync(Path.GetRelativePath(_ws.CriteriaDir, file))).Select(c => c.Id));
        }
        return ids.OrderBy(i => i, StringComparer.Ordinal).ToArray();
    }

    private Task<(int Exit, string Out, string Err)> IngestAAsync(params string[] args)
    {
        _ws.Response(
            new { id = "AC-LOGIN-001", text = "A one MUST hold", rfc2119 = "MUST" },
            new { id = "AC-LOGIN-002", text = "A two MUST hold", rfc2119 = "MUST" });
        return _ws.RunAsync(["ai", "ingest-criteria", "--doc", DocA, "--from", ".spectra/criteria.json", "--output-format", "json", .. args]);
    }

    [Fact]
    public async Task Compile_OffersOnlyTheDocsOwnCriteria()
    {
        await SeedAsync();

        var (exit, output, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", DocA);

        Assert.True(exit == 0, err);
        Assert.Contains("AC-LOGIN-001", output);
        Assert.Contains("AC-LOGIN-002", output);
        Assert.DoesNotContain("AC-LOGIN-003", output);
        Assert.DoesNotContain("AC-LOGIN-009", output);
    }

    [Fact]
    public async Task Ingest_WithThePromptsIds_SucceedsAndKeepsThem()
    {
        await SeedAsync();

        var (exit, _, err) = await IngestAAsync();

        Assert.True(exit == 0, err);
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002"], await IdsInAsync("docs/a/login.criteria.yaml"));
    }

    [Fact]
    public async Task ReIngest_TakesTheDocsCriteriaOutOfTheSharedFile()
    {
        await SeedAsync();

        var (exit, _, err) = await IngestAAsync();

        Assert.True(exit == 0, err);
        Assert.Equal(["AC-LOGIN-003", "AC-LOGIN-009"], await IdsInAsync(Shared));
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002", "AC-LOGIN-003", "AC-LOGIN-009"], await AllIdsOnDiskAsync());
    }

    [Fact]
    public async Task ApplyMove_CarriesOnlyTheMovedDocsCriteria()
    {
        await SeedAsync();
        File.Delete(_ws.PathOf(DocB));
        _ws.WriteDoc("docs/c/login.md", BContent);

        var (exit, _, err) = await _ws.RunAsync("docs", "apply-move", "--from", DocB, "--to", "docs/c/login.md", "--output-format", "json");

        Assert.True(exit == 0, err);
        var moved = await _ws.ReadCriteriaAsync("docs/c/login.criteria.yaml");
        var only = Assert.Single(moved);
        Assert.Equal("AC-LOGIN-003", only.Id);
        Assert.Equal("docs/c/login.md", only.SourceDoc);
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002", "AC-LOGIN-009"], await IdsInAsync(Shared));
    }

    [Fact]
    public async Task Retire_TakesTheDocsCriteriaOutOfTheSharedFile()
    {
        await SeedAsync();
        File.Delete(_ws.PathOf(DocB));

        var (exit, _, err) = await _ws.RunAsync("docs", "retire", "--doc", DocB, "--output-format", "json");

        Assert.True(exit == 0, err);
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002", "AC-LOGIN-009"], await IdsInAsync(Shared));
        Assert.DoesNotContain("AC-LOGIN-003", await AllIdsOnDiskAsync());
    }

    [Fact]
    public async Task LastDocLeaving_DeletesTheEmptyUnreferencedFile()
    {
        await SeedAsync(withUnowned: false);
        File.Delete(_ws.PathOf(DocB));

        // B retires while the file is still shared; A is then the last doc to leave it.
        var (exitB, _, errB) = await _ws.RunAsync("docs", "retire", "--doc", DocB, "--output-format", "json");
        var (exitA, _, errA) = await IngestAAsync();

        Assert.True(exitB == 0, errB);
        Assert.True(exitA == 0, errA);
        Assert.False(File.Exists(Path.Combine(_ws.CriteriaDir, Shared)));
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002"], await AllIdsOnDiskAsync());
    }

    [Fact]
    public async Task UnownedCriteria_StayWhileShared_AndAreReportedWhenTheLastDocTakesTheFile()
    {
        await SeedAsync(withUnowned: true);
        File.Delete(_ws.PathOf(DocB));

        // While shared, no document's operation touches the unowned criterion.
        var (exitB, _, errB) = await _ws.RunAsync("docs", "retire", "--doc", DocB, "--output-format", "json");
        Assert.True(exitB == 0, errB);
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002", "AC-LOGIN-009"], await IdsInAsync(Shared));

        // A is now the file's only document (the 079 rule): its re-ingest reports the unowned criterion as removed.
        var (exitA, output, errA) = await IngestAAsync();
        Assert.True(exitA == 0, errA);
        var removed = CriteriaTestWorkspace.Json(output).GetProperty("changeset").GetProperty("removed");
        Assert.Equal(["AC-LOGIN-009"], removed.EnumerateArray().Select(r => r.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task DryRuns_WriteNothing()
    {
        await SeedAsync();
        File.Delete(_ws.PathOf(DocB));
        _ws.WriteDoc("docs/c/login.md", BContent);
        _ws.Response(
            new { id = "AC-LOGIN-001", text = "A one MUST hold", rfc2119 = "MUST" },
            new { id = "AC-LOGIN-002", text = "A two MUST hold", rfc2119 = "MUST" });
        var before = _ws.Snapshot();

        var ingest = await IngestAAsync("--dry-run");
        var move = await _ws.RunAsync("docs", "apply-move", "--from", DocB, "--to", "docs/c/login.md", "--dry-run", "--output-format", "json");
        var retire = await _ws.RunAsync("docs", "retire", "--doc", DocB, "--dry-run", "--output-format", "json");

        Assert.True(ingest.Exit == 0, ingest.Err);
        Assert.True(move.Exit == 0, move.Err);
        Assert.True(retire.Exit == 0, retire.Err);
        Assert.Equal(before, _ws.Snapshot());
    }
}
