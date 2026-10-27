using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 081 C1 (FR-001–FR-003, SC-001): criteria and index entries whose <c>source_doc</c> was stored as an absolute
/// path inside the repository (or with <c>\</c> / <c>./</c>) still belong to their document — in a shared file's
/// ownership decisions and in every index lookup (compile, <c>docs changed</c>, the write commands' workspace).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class AbsoluteSourceDocTests : IDisposable
{
    private const string Shared = "login.criteria.yaml";
    private const string DocA = "docs/a/login.md";
    private const string DocB = "docs/b/login.md";

    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private string Absolute(string relative) => _ws.PathOf(relative).Replace('\\', '/');

    /// <summary>A shared file: A's criteria store an absolute source_doc; B's the given form.</summary>
    private async Task SeedSharedAsync(string bSourceDoc)
    {
        _ws.WriteDoc(DocA, "# Login A\n\nUsers MUST see A.\n");
        var bHash = await FileHasher.ComputeFileHashAsync(_ws.WriteDoc(DocB, "# Login B\n\nUsers MUST see B.\n"));
        _ws.WriteCriteriaFile(Shared, $"""
            criteria:
            - id: AC-LOGIN-001
              text: A one MUST hold
              source_doc: '{Absolute(DocA)}'
            - id: AC-LOGIN-002
              text: A two MUST hold
              source_doc: '{Absolute(DocA)}'
            - id: AC-LOGIN-003
              text: B three MUST hold
              source_doc: '{bSourceDoc}'
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

    /// <summary>A per-doc criteria file whose index entry stores the document path absolute.</summary>
    private async Task SeedAbsoluteIndexEntryAsync()
    {
        var hash = await FileHasher.ComputeFileHashAsync(_ws.WriteDoc("docs/x.md", "# X\n\nX MUST work.\n"));
        _ws.WriteCriteriaFile("docs/x.criteria.yaml", """
            criteria:
            - id: AC-X-001
              text: X MUST work
              source_doc: docs/x.md
            """);
        _ws.WriteIndexYaml($"""
            version: 1
            sources:
            - file: docs/x.criteria.yaml
              source_doc: '{Absolute("docs/x.md")}'
              source_type: document
              doc_hash: {hash}
              criteria_count: 1
            """);
    }

    private async Task<string[]> IdsInAsync(string file) =>
        (await _ws.ReadCriteriaAsync(file)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();

    // ---------- shared file: per-criterion source_doc stored absolute ----------

    [Fact]
    public async Task SharedFile_Compile_OffersTheDocsAbsolutePathCriteria()
    {
        await SeedSharedAsync(DocB);

        var (exit, output, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", DocA);

        Assert.True(exit == 0, err);
        Assert.Contains("AC-LOGIN-001", output);
        Assert.Contains("AC-LOGIN-002", output);
        Assert.DoesNotContain("AC-LOGIN-003", output);
    }

    [Fact]
    public async Task SharedFile_Ingest_KeepsTheIds_AndTakesThemOutOfTheSharedFile()
    {
        await SeedSharedAsync(DocB);

        var (exit, _, err) = await _ws.IngestAsync(DocA,
            new { id = "AC-LOGIN-001", text = "A one MUST hold", rfc2119 = "MUST" },
            new { id = "AC-LOGIN-002", text = "A two MUST hold", rfc2119 = "MUST" });

        Assert.True(exit == 0, err);
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002"], await IdsInAsync("docs/a/login.criteria.yaml"));
        Assert.Equal(["AC-LOGIN-003"], await IdsInAsync(Shared));
    }

    [Theory]
    [InlineData(true)]   // B's criteria store an absolute source_doc (fails on 1c8d8de)
    [InlineData(false)]  // ./docs\b\login.md (already matched in 080; guard)
    public async Task SharedFile_Retire_RemovesOnlyTheRetiredDocsCriteria(bool absoluteB)
    {
        await SeedSharedAsync(absoluteB ? Absolute(DocB) : "./docs\\b\\login.md");
        File.Delete(_ws.PathOf(DocB));

        var (exit, _, err) = await _ws.RunAsync("docs", "retire", "--doc", DocB, "--output-format", "json");

        Assert.True(exit == 0, err);
        Assert.Equal(["AC-LOGIN-001", "AC-LOGIN-002"], await IdsInAsync(Shared));
    }

    // ---------- index entry whose source_doc is absolute ----------

    [Fact]
    public async Task AbsoluteIndexEntry_DocsChanged_ReportsTheDocUnchanged()
    {
        await SeedAbsoluteIndexEntryAsync();

        var (exit, output, err) = await _ws.RunAsync("docs", "changed", "--output-format", "json");

        Assert.True(exit == 0, err);
        var changed = CriteriaTestWorkspace.Json(output).GetProperty("changed");
        Assert.DoesNotContain(changed.EnumerateArray(), e =>
            e.GetProperty("status").GetString() is "new" or "deleted" or "moved");
    }

    [Fact]
    public async Task AbsoluteIndexEntry_DryRunIngest_FindsTheDoc_AndAcceptsItsExistingId()
    {
        await SeedAbsoluteIndexEntryAsync();

        _ws.Response(new { id = "AC-X-001", text = "X MUST work", rfc2119 = "MUST" });
        var (exit, _, err) = await _ws.RunAsync(
            "ai", "ingest-criteria", "--doc", "docs/x.md", "--from", ".spectra/criteria.json", "--dry-run", "--output-format", "json");

        Assert.True(exit == 0, err);
    }

    [Fact]
    public async Task AbsoluteIndexEntry_Compile_OffersTheExistingId()
    {
        await SeedAbsoluteIndexEntryAsync();

        var (exit, output, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/x.md");

        Assert.True(exit == 0, err);
        Assert.Contains("AC-X-001", output);
    }
}
