using Spectra.Core.Models;

namespace Spectra.Core.Validation;

/// <summary>
/// Validates that test IDs are unique across all tests.
/// </summary>
public sealed class IdUniquenessValidator
{
    /// <summary>
    /// Validates that all test IDs are unique.
    /// </summary>
    public ValidationResult Validate(IEnumerable<TestCase> testCases)
    {
        ArgumentNullException.ThrowIfNull(testCases);

        var errors = new List<ValidationError>();
        var seenIds = new Dictionary<string, string>(); // ID -> first file path

        foreach (var testCase in testCases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id))
            {
                continue; // Skip tests without IDs - TestValidator handles this
            }

            if (seenIds.TryGetValue(testCase.Id, out var existingPath))
            {
                errors.Add(new ValidationError(
                    "DUPLICATE_ID",
                    $"Test ID '{testCase.Id}' is already used in '{existingPath}'",
                    testCase.FilePath,
                    testCase.Id));
            }
            else
            {
                seenIds[testCase.Id] = testCase.FilePath;
            }
        }

        return new ValidationResult
        {
            Errors = errors,
            Warnings = []
        };
    }

    /// <summary>
    /// Validates uniqueness within a single suite.
    /// </summary>
    public ValidationResult ValidateSuite(string suiteName, IEnumerable<TestCase> testCases)
    {
        var result = Validate(testCases);

        // Update error messages to include suite context
        var suiteErrors = result.Errors.Select(e => new ValidationError(
            e.Code,
            $"[{suiteName}] {e.Message}",
            e.FilePath,
            e.TestId)).ToList();

        return new ValidationResult
        {
            Errors = suiteErrors,
            Warnings = result.Warnings
        };
    }
}
