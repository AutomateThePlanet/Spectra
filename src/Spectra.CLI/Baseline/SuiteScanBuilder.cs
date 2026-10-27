using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Baseline;

/// <summary>One test file of a suite: its full path and the parsed test, or null when it failed to parse.</summary>
public sealed record SuiteFile(string Path, TestCase? Test);

/// <summary>
/// Spec 099 F1: reads a suite's test files once and builds the <see cref="SuiteScan"/> the baseline needs from them.
/// An ingest reads the suite before persisting and passes the files it wrote to <see cref="Build"/>, so the scan
/// equals the one <see cref="BaselineStore.ScanSuiteAsync"/> would make after the write without parsing the suite again.
/// </summary>
public static class SuiteScanBuilder
{
    /// <summary>How the file system compares paths: a write to <c>TC-1.md</c> overwrites <c>tc-1.md</c> on Windows and macOS.</summary>
    public static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Every <c>*.md</c> of the suite folder not starting with <c>_</c> (not recursive), in the order the file system
    /// lists them, each parsed once. Empty when the folder doesn't exist. Spec 099 F3: <paramref name="captureLayout"/>
    /// only when the caller may write one of the tests back.
    /// </summary>
    public static async Task<IReadOnlyList<SuiteFile>> ReadAsync(string suitePath, bool captureLayout, CancellationToken ct)
    {
        var files = new List<SuiteFile>();
        if (!Directory.Exists(suitePath))
        {
            return files;
        }

        var parser = new TestCaseParser(captureLayout);
        foreach (var file in Directory.GetFiles(suitePath, "*.md").Where(f => !System.IO.Path.GetFileName(f).StartsWith('_')))
        {
            var parsed = parser.Parse(await File.ReadAllTextAsync(file, ct), System.IO.Path.GetRelativePath(suitePath, file));
            files.Add(new SuiteFile(file, parsed.IsSuccess ? parsed.Value! : null));
        }

        return files;
    }

    /// <summary>The tests that parsed, in the order of <paramref name="files"/>.</summary>
    public static List<TestCase> Tests(IEnumerable<SuiteFile> files) =>
        files.Where(f => f.Test is not null).Select(f => f.Test!).ToList();

    /// <summary>
    /// The suite scan of <paramref name="files"/> after <paramref name="written"/> were written: a written file replaces
    /// any file with the same path (the file system's case rule) or is added. Tests are ordered by ID and unparseable
    /// files by path (ordinal, repo-relative with <c>/</c>), as <see cref="BaselineStore.ScanSuiteAsync"/> orders them.
    /// </summary>
    public static SuiteScan Build(string repoRoot, IEnumerable<SuiteFile> files, IEnumerable<SuiteFile>? written = null)
    {
        var byPath = new Dictionary<string, SuiteFile>(PathComparer);
        foreach (var file in files)
        {
            byPath[file.Path] = file;
        }

        foreach (var file in written ?? [])
        {
            // Keep the existing key's spelling: the file system keeps the original name when it overwrites.
            var key = byPath.Keys.FirstOrDefault(k => PathComparer.Equals(k, file.Path)) ?? file.Path;
            byPath[key] = file with { Path = key };
        }

        var ordered = byPath.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        var tests = ordered.Where(f => f.Test is not null).Select(f => f.Test!)
            .OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        var unparseable = ordered.Where(f => f.Test is null).Select(f => BaselineStore.Relative(repoRoot, f.Path)).ToList();
        return new SuiteScan(tests, tests.Select(t => t.Id).ToHashSet(StringComparer.Ordinal), unparseable);
    }
}
