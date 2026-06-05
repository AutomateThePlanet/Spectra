using Spectra.CLI.Generation;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 053 — US3 fail-loud boundary tests. Malformed/truncated/empty/schema-violating
/// content yields a specific error and persists NOTHING; valid content persists through the
/// unchanged TestPersistenceService. Token-free.
/// </summary>
public sealed class GeneratedTestIngestorTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;
    private const string Suite = "reporting";

    public GeneratedTestIngestorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "spectra-ingest-" + Guid.NewGuid().ToString("N"));
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best effort */ }
    }

    private static GeneratedTestIngestor NewIngestor() =>
        new(new TestPersistenceService(new TestFileWriter(), new IndexGenerator(), new IndexWriter()));

    private static string ValidTestJson(string id = "TC-901", string title = "Export to PDF") => $$"""
        [
          {
            "id": "{{id}}",
            "title": "{{title}}",
            "priority": "high",
            "steps": ["Open report", "Click export"],
            "expected_result": "A PDF file is produced",
            "criteria": ["AC-REPORTING-001"]
          }
        ]
        """;

    private async Task AssertNothingPersistedAsync(IngestResult result, string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(result.PersistedTests);
        // Zero-persistence invariant: the suite dir must not have been created/written.
        var suiteDir = Path.Combine(_testsPath, Suite);
        Assert.False(Directory.Exists(suiteDir) && Directory.EnumerateFileSystemEntries(suiteDir).Any(),
            "Boundary failure must persist nothing.");
        await Task.CompletedTask;
    }

    // ---------- fail-loud cases (T008) ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ingest_EmptyContent_FailsLoud(string? content)
    {
        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, [], default);
        await AssertNothingPersistedAsync(result, IngestErrorCode.EmptyContent);
    }

    [Fact]
    public async Task Ingest_NonJson_FailsMalformed()
    {
        var result = await NewIngestor().IngestAsync("this is not json at all", _testsPath, Suite, [], default);
        await AssertNothingPersistedAsync(result, IngestErrorCode.EmptyContent); // no '[' present
    }

    [Fact]
    public async Task Ingest_BrokenJsonArray_FailsMalformed()
    {
        // Has a '[' and a ']' but is not parseable as an array.
        var result = await NewIngestor().IngestAsync("[ {\"id\": } ]", _testsPath, Suite, [], default);
        await AssertNothingPersistedAsync(result, IngestErrorCode.MalformedJson);
    }

    [Fact]
    public async Task Ingest_TruncatedArray_FailsLoud_NoSalvage()
    {
        // Opened '[' with one complete object then cut off — the OLD code would salvage this.
        var truncated = "[ {\"id\":\"TC-901\",\"title\":\"A\",\"expected_result\":\"ok\"}, {\"id\":\"TC-902\",";
        var result = await NewIngestor().IngestAsync(truncated, _testsPath, Suite, [], default);
        await AssertNothingPersistedAsync(result, IngestErrorCode.Truncated);
    }

    [Fact]
    public async Task Ingest_ArrayWithNoValidTests_FailsNoTests()
    {
        // Objects lacking id/title parse to null → zero valid tests.
        var result = await NewIngestor().IngestAsync("[ { \"foo\": 1 }, { \"bar\": 2 } ]", _testsPath, Suite, [], default);
        await AssertNothingPersistedAsync(result, IngestErrorCode.NoTests);
    }

    [Fact]
    public async Task Ingest_SchemaViolatingTest_FailsSchemaInvalid_WithEchoedCodes()
    {
        // Parses fine (id+title present) but the id violates the TC-### pattern.
        var content = ValidTestJson(id: "BADID");
        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, [], default);

        await AssertNothingPersistedAsync(result, IngestErrorCode.SchemaInvalid);
        Assert.Contains(result.Errors, e => e.Contains("INVALID_ID_FORMAT"));
        Assert.Contains(result.Errors, e => e.Contains("BADID"));
    }

    [Fact]
    public async Task Ingest_BatchAtomicity_OneInvalidTest_FailsWholeBatch()
    {
        // One valid, one invalid → whole batch fails, nothing written.
        var content = """
            [
              { "id": "TC-901", "title": "Valid", "expected_result": "ok", "steps": ["a"] },
              { "id": "NOPE",    "title": "Invalid id", "expected_result": "ok", "steps": ["a"] }
            ]
            """;
        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, [], default);
        await AssertNothingPersistedAsync(result, IngestErrorCode.SchemaInvalid);
    }

    // ---------- happy path (T009) ----------

    [Fact]
    public async Task Ingest_ValidContent_Persists_AndRegeneratesIndex()
    {
        var result = await NewIngestor().IngestAsync(ValidTestJson(), _testsPath, Suite, [], default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.PersistedTests);
        Assert.Equal("TC-901", result.PersistedTests[0].Id);

        var suiteDir = Path.Combine(_testsPath, Suite);
        Assert.True(File.Exists(Path.Combine(suiteDir, "TC-901.md")), "test file written");
        Assert.True(File.Exists(Path.Combine(suiteDir, "_index.json")), "index regenerated");
    }

    [Fact]
    public async Task ParseAndValidate_IsPure_NoPersistenceNeeded()
    {
        var ok = GeneratedTestIngestor.ParseAndValidate(ValidTestJson(), new TestValidator());
        Assert.True(ok.IsSuccess);
        Assert.Single(ok.PersistedTests); // "validated, not yet written"

        var bad = GeneratedTestIngestor.ParseAndValidate("nope", new TestValidator());
        Assert.False(bad.IsSuccess);
    }
}
