using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class CriteriaIndexReaderWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CriteriaIndexReader _reader;
    private readonly CriteriaIndexWriter _writer;

    public CriteriaIndexReaderWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"criteria-idx-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _reader = new CriteriaIndexReader();
        _writer = new CriteriaIndexWriter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsEmptyIndex()
    {
        var path = Path.Combine(_tempDir, "nonexistent.yaml");

        var result = await _reader.ReadAsync(path);

        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.Equal(0, result.TotalCriteria);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task ReadAsync_ValidIndex_ParsesCorrectly()
    {
        var path = Path.Combine(_tempDir, "index.yaml");
        var yaml = """
            version: 2
            total_criteria: 15
            sources:
              - file: checkout.criteria.yaml
                source_doc: docs/checkout.md
                source_type: document
                doc_hash: abc123
                criteria_count: 15
                last_extracted: 2026-01-15T10:00:00Z
            """;
        await File.WriteAllTextAsync(path, yaml);

        var result = await _reader.ReadAsync(path);

        Assert.Equal(2, result.Version);
        Assert.Equal(15, result.TotalCriteria);
        Assert.Single(result.Sources);
        var source = result.Sources[0];
        Assert.Equal("checkout.criteria.yaml", source.File);
        Assert.Equal("docs/checkout.md", source.SourceDoc);
        Assert.Equal("document", source.SourceType);
        Assert.Equal("abc123", source.DocHash);
        Assert.Equal(15, source.CriteriaCount);
        Assert.NotNull(source.LastExtracted);
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "roundtrip.yaml");
        var index = new CriteriaIndex
        {
            Version = 1,
            Sources =
            [
                new CriteriaSource
                {
                    File = "auth.criteria.yaml",
                    SourceDoc = "docs/auth.md",
                    SourceType = "document",
                    DocHash = "hash1",
                    CriteriaCount = 5,
                    LastExtracted = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
                },
                new CriteriaSource
                {
                    File = "payments.criteria.yaml",
                    SourceDoc = "docs/payments.md",
                    SourceType = "document",
                    DocHash = "hash2",
                    CriteriaCount = 8,
                    LastExtracted = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        await _writer.WriteAsync(path, index);
        var result = await _reader.ReadAsync(path);

        Assert.Equal(1, result.Version);
        Assert.Equal(13, result.TotalCriteria);
        Assert.Equal(2, result.Sources.Count);

        Assert.Equal("auth.criteria.yaml", result.Sources[0].File);
        Assert.Equal("docs/auth.md", result.Sources[0].SourceDoc);
        Assert.Equal("hash1", result.Sources[0].DocHash);
        Assert.Equal(5, result.Sources[0].CriteriaCount);

        Assert.Equal("payments.criteria.yaml", result.Sources[1].File);
        Assert.Equal("docs/payments.md", result.Sources[1].SourceDoc);
        Assert.Equal("hash2", result.Sources[1].DocHash);
        Assert.Equal(8, result.Sources[1].CriteriaCount);
    }

    [Fact]
    public async Task ReadAsync_EmptyFile_ReturnsEmptyIndex()
    {
        var path = Path.Combine(_tempDir, "empty.yaml");
        await File.WriteAllTextAsync(path, "");

        var result = await _reader.ReadAsync(path);

        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.Equal(0, result.TotalCriteria);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task ReadAsync_MixedSourceTypes_ParsesAll()
    {
        var path = Path.Combine(_tempDir, "mixed.yaml");
        var yaml = """
            version: 1
            total_criteria: 30
            sources:
              - file: auth.criteria.yaml
                source_doc: docs/auth.md
                source_type: document
                criteria_count: 10
              - file: jira-sprint42.criteria.yaml
                source_doc: PROJ-123
                source_type: jira
                criteria_count: 12
                imported_at: 2026-02-20T08:00:00Z
              - file: manual.criteria.yaml
                source_type: manual
                criteria_count: 8
            """;
        await File.WriteAllTextAsync(path, yaml);

        var result = await _reader.ReadAsync(path);

        Assert.Equal(3, result.Sources.Count);

        Assert.Equal("document", result.Sources[0].SourceType);
        Assert.Equal("docs/auth.md", result.Sources[0].SourceDoc);

        Assert.Equal("jira", result.Sources[1].SourceType);
        Assert.Equal("PROJ-123", result.Sources[1].SourceDoc);
        Assert.NotNull(result.Sources[1].ImportedAt);

        Assert.Equal("manual", result.Sources[2].SourceType);
        Assert.Null(result.Sources[2].SourceDoc);
    }

    [Fact]
    public async Task WriteAsync_RecalculatesTotalCriteria()
    {
        var path = Path.Combine(_tempDir, "recalc.yaml");
        var index = new CriteriaIndex
        {
            TotalCriteria = 999, // intentionally wrong
            Sources =
            [
                new CriteriaSource { File = "a.yaml", CriteriaCount = 10 },
                new CriteriaSource { File = "b.yaml", CriteriaCount = 20 }
            ]
        };

        await _writer.WriteAsync(path, index);
        var result = await _reader.ReadAsync(path);

        Assert.Equal(30, result.TotalCriteria);
    }
}
