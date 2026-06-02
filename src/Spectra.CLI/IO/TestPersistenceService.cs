using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.IO;

/// <summary>
/// Single entry point for persisting generated tests to a suite. Owns the
/// write-then-index invariant: every successful call writes the test files
/// AND regenerates the suite's _index.json from the supplied full set.
/// </summary>
public sealed class TestPersistenceService
{
    private readonly TestFileWriter _writer;
    private readonly IndexGenerator _indexGenerator;
    private readonly IndexWriter _indexWriter;

    public TestPersistenceService(
        TestFileWriter writer,
        IndexGenerator indexGenerator,
        IndexWriter indexWriter)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(indexGenerator);
        ArgumentNullException.ThrowIfNull(indexWriter);

        _writer = writer;
        _indexGenerator = indexGenerator;
        _indexWriter = indexWriter;
    }

    /// <summary>
    /// Writes <paramref name="testsToWrite"/> as .md files under
    /// {testsPath}/{suite}/ and regenerates {testsPath}/{suite}/_index.json
    /// from <paramref name="allTestsForIndex"/> (existing + new, deduped by id).
    /// </summary>
    public async Task PersistAsync(
        string testsPath,
        string suite,
        IReadOnlyList<TestCase> testsToWrite,
        IReadOnlyList<TestCase> allTestsForIndex,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suite);
        ArgumentNullException.ThrowIfNull(testsToWrite);
        ArgumentNullException.ThrowIfNull(allTestsForIndex);

        foreach (var tc in testsToWrite)
        {
            var path = TestFileWriter.GetFilePath(testsPath, suite, tc.Id);
            await _writer.WriteAsync(path, tc, ct);
        }

        var index = _indexGenerator.Generate(suite, allTestsForIndex);
        var indexPath = Path.Combine(testsPath, suite, "_index.json");
        await _indexWriter.WriteAsync(indexPath, index, ct);
    }
}
