using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Interactive;

/// <summary>
/// Scans for test suites and collects summary information.
/// </summary>
public sealed class SuiteScanner
{
    /// <summary>
    /// Scans the tests directory for suites with their test counts and metadata.
    /// </summary>
    public async Task<IReadOnlyList<SuiteSummary>> ScanSuitesAsync(
        string testsDirectory,
        CancellationToken ct = default)
    {
        var summaries = new List<SuiteSummary>();

        if (!Directory.Exists(testsDirectory))
        {
            return summaries;
        }

        var suiteDirs = Directory.GetDirectories(testsDirectory)
            .Where(d => !Path.GetFileName(d).StartsWith('_'))
            .OrderBy(Path.GetFileName);

        foreach (var suiteDir in suiteDirs)
        {
            ct.ThrowIfCancellationRequested();

            var suiteName = Path.GetFileName(suiteDir);
            var testFiles = Directory.GetFiles(suiteDir, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith('_'))
                .ToList();

            var testCount = testFiles.Count;
            DateTimeOffset? lastUpdated = null;

            if (testFiles.Count > 0)
            {
                var mostRecent = testFiles
                    .Select(f => new FileInfo(f).LastWriteTimeUtc)
                    .Max();

                lastUpdated = new DateTimeOffset(mostRecent, TimeSpan.Zero);
            }

            summaries.Add(new SuiteSummary
            {
                Name = suiteName,
                Path = suiteDir,
                TestCount = testCount,
                LastUpdated = lastUpdated
            });
        }

        return summaries;
    }

    /// <summary>
    /// Loads all tests from a specific suite.
    /// </summary>
    public async Task<IReadOnlyList<TestCase>> LoadSuiteTestsAsync(
        string suitePath,
        string testsBasePath,
        CancellationToken ct = default)
    {
        var tests = new List<TestCase>();

        if (!Directory.Exists(suitePath))
        {
            return tests;
        }

        var parser = new TestCaseParser();
        var files = Directory.GetFiles(suitePath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith('_'));

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(file, ct);
            var relativePath = Path.GetRelativePath(testsBasePath, file);
            var result = parser.Parse(content, relativePath);

            if (result.IsSuccess)
            {
                tests.Add(result.Value!);
            }
        }

        return tests;
    }

    /// <summary>
    /// Checks if a suite exists.
    /// </summary>
    public bool SuiteExists(string testsDirectory, string suiteName)
    {
        var suitePath = Path.Combine(testsDirectory, suiteName);
        return Directory.Exists(suitePath);
    }

    /// <summary>
    /// Creates a new suite directory.
    /// </summary>
    public void CreateSuite(string testsDirectory, string suiteName)
    {
        var suitePath = Path.Combine(testsDirectory, suiteName);
        Directory.CreateDirectory(suitePath);
    }
}
