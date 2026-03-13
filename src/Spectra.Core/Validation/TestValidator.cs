using System.Text.RegularExpressions;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Validation;

/// <summary>
/// Validates individual test cases against schema rules.
/// </summary>
public sealed partial class TestValidator
{
    private readonly ValidationConfig _config;

    [GeneratedRegex(@"^TC-\d{3,}$")]
    private static partial Regex DefaultIdPatternRegex();

    public TestValidator(ValidationConfig? config = null)
    {
        _config = config ?? new ValidationConfig();
    }

    /// <summary>
    /// Validates a single test case.
    /// </summary>
    public ValidationResult Validate(TestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validate ID format
        ValidateId(testCase, errors);

        // Validate required fields
        ValidateRequiredFields(testCase, errors);

        // Validate priority
        ValidatePriority(testCase, errors);

        // Validate steps count
        ValidateSteps(testCase, warnings);

        // Validate expected result
        ValidateExpectedResult(testCase, errors);

        return new ValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Validates multiple test cases.
    /// </summary>
    public ValidationResult ValidateAll(IEnumerable<TestCase> testCases)
    {
        var results = testCases.Select(Validate).ToList();
        return ValidationResult.Combine(results);
    }

    private void ValidateId(TestCase testCase, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(testCase.Id))
        {
            errors.Add(new ValidationError(
                "MISSING_ID",
                "Test case is missing required field 'id'",
                testCase.FilePath,
                testCase.Id));
            return;
        }

        var pattern = new Regex(_config.IdPattern);
        if (!pattern.IsMatch(testCase.Id))
        {
            errors.Add(new ValidationError(
                "INVALID_ID_FORMAT",
                $"Test ID '{testCase.Id}' does not match pattern '{_config.IdPattern}'",
                testCase.FilePath,
                testCase.Id));
        }
    }

    private void ValidateRequiredFields(TestCase testCase, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(testCase.Title))
        {
            errors.Add(new ValidationError(
                "MISSING_TITLE",
                "Test case is missing required field 'title'",
                testCase.FilePath,
                testCase.Id));
        }
    }

    private void ValidatePriority(TestCase testCase, List<ValidationError> errors)
    {
        var priorityString = testCase.Priority.ToString().ToLowerInvariant();
        if (!_config.AllowedPriorities.Contains(priorityString))
        {
            errors.Add(new ValidationError(
                "INVALID_PRIORITY",
                $"Invalid priority '{priorityString}'. Allowed: {string.Join(", ", _config.AllowedPriorities)}",
                testCase.FilePath,
                testCase.Id));
        }
    }

    private void ValidateSteps(TestCase testCase, List<ValidationWarning> warnings)
    {
        if (testCase.Steps.Count > _config.MaxSteps)
        {
            warnings.Add(new ValidationWarning(
                "TOO_MANY_STEPS",
                $"Test has {testCase.Steps.Count} steps, which exceeds the recommended maximum of {_config.MaxSteps}",
                testCase.FilePath,
                testCase.Id));
        }

        if (testCase.Steps.Count == 0)
        {
            warnings.Add(new ValidationWarning(
                "NO_STEPS",
                "Test case has no steps defined",
                testCase.FilePath,
                testCase.Id));
        }
    }

    private void ValidateExpectedResult(TestCase testCase, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(testCase.ExpectedResult))
        {
            errors.Add(new ValidationError(
                "MISSING_EXPECTED_RESULT",
                "Test case is missing required field 'expected_result'",
                testCase.FilePath,
                testCase.Id));
        }
    }
}
