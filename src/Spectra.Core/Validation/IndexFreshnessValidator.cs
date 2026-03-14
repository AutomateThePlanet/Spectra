using Spectra.Core.Models;

namespace Spectra.Core.Validation;

/// <summary>
/// Validates that the _index.json file is up to date with test files.
/// </summary>
public sealed class IndexFreshnessValidator
{
    /// <summary>
    /// Validates index freshness against parsed test cases.
    /// </summary>
    public ValidationResult Validate(
        MetadataIndex index,
        IEnumerable<TestCase> actualTests,
        string suitePath)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(actualTests);

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        var actualTestList = actualTests.ToList();
        var indexedIds = new HashSet<string>(index.Tests.Select(t => t.Id));
        var actualIds = new HashSet<string>(actualTestList.Select(t => t.Id));

        // Check for tests in index but not on disk
        var orphanedInIndex = indexedIds.Except(actualIds).ToList();
        foreach (var id in orphanedInIndex)
        {
            errors.Add(new ValidationError(
                "INDEX_ORPHAN",
                $"Test '{id}' is in index but file not found",
                Path.Combine(suitePath, "_index.json"),
                id));
        }

        // Check for tests on disk but not in index
        var missingFromIndex = actualIds.Except(indexedIds).ToList();
        foreach (var id in missingFromIndex)
        {
            var testCase = actualTestList.First(t => t.Id == id);
            errors.Add(new ValidationError(
                "INDEX_MISSING",
                $"Test '{id}' exists but is not in index",
                testCase.FilePath,
                id));
        }

        // Check for metadata mismatches
        foreach (var indexEntry in index.Tests)
        {
            var actualTest = actualTestList.FirstOrDefault(t => t.Id == indexEntry.Id);
            if (actualTest is null)
            {
                continue; // Already reported as orphan
            }

            // Check title mismatch
            if (indexEntry.Title != actualTest.Title)
            {
                warnings.Add(new ValidationWarning(
                    "INDEX_TITLE_MISMATCH",
                    $"Index title '{indexEntry.Title}' differs from actual '{actualTest.Title}'",
                    actualTest.FilePath,
                    actualTest.Id));
            }

            // Check priority mismatch
            var actualPriority = actualTest.Priority.ToString().ToLowerInvariant();
            if (indexEntry.Priority.ToLowerInvariant() != actualPriority)
            {
                warnings.Add(new ValidationWarning(
                    "INDEX_PRIORITY_MISMATCH",
                    $"Index priority '{indexEntry.Priority}' differs from actual '{actualPriority}'",
                    actualTest.FilePath,
                    actualTest.Id));
            }
        }

        // Check count mismatch
        if (index.TestCount != actualTestList.Count)
        {
            warnings.Add(new ValidationWarning(
                "INDEX_COUNT_MISMATCH",
                $"Index reports {index.TestCount} tests but found {actualTestList.Count}",
                Path.Combine(suitePath, "_index.json")));
        }

        return new ValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Checks if index needs to be rebuilt (quick check based on file timestamps).
    /// </summary>
    public bool NeedsRebuild(string indexPath, IEnumerable<string> testFilePaths)
    {
        if (!File.Exists(indexPath))
        {
            return true;
        }

        var indexTime = File.GetLastWriteTimeUtc(indexPath);

        foreach (var testPath in testFilePaths)
        {
            if (!File.Exists(testPath))
            {
                return true; // Test file removed
            }

            if (File.GetLastWriteTimeUtc(testPath) > indexTime)
            {
                return true; // Test file modified after index
            }
        }

        return false;
    }
}
