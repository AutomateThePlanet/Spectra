using Spectra.Core.Models;

namespace Spectra.Core.Validation;

/// <summary>
/// Validates that depends_on references point to existing test IDs.
/// </summary>
public sealed class DependsOnValidator
{
    /// <summary>
    /// Validates all depends_on references.
    /// </summary>
    public ValidationResult Validate(IEnumerable<TestCase> testCases)
    {
        ArgumentNullException.ThrowIfNull(testCases);

        var testList = testCases.ToList();
        var allIds = new HashSet<string>(testList
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .Select(t => t.Id));

        var errors = new List<ValidationError>();

        foreach (var testCase in testList)
        {
            if (string.IsNullOrWhiteSpace(testCase.DependsOn))
            {
                continue;
            }

            if (!allIds.Contains(testCase.DependsOn))
            {
                errors.Add(new ValidationError(
                    "INVALID_DEPENDS_ON",
                    $"Test '{testCase.Id}' depends on '{testCase.DependsOn}' which does not exist",
                    testCase.FilePath,
                    testCase.Id));
            }

            // Check for self-reference
            if (testCase.DependsOn == testCase.Id)
            {
                errors.Add(new ValidationError(
                    "SELF_REFERENCE",
                    $"Test '{testCase.Id}' cannot depend on itself",
                    testCase.FilePath,
                    testCase.Id));
            }
        }

        // Check for circular dependencies
        var circularErrors = DetectCircularDependencies(testList);
        errors.AddRange(circularErrors);

        return new ValidationResult
        {
            Errors = errors,
            Warnings = []
        };
    }

    private static List<ValidationError> DetectCircularDependencies(List<TestCase> testCases)
    {
        var errors = new List<ValidationError>();
        var dependencyMap = testCases
            .Where(t => !string.IsNullOrWhiteSpace(t.Id) && !string.IsNullOrWhiteSpace(t.DependsOn))
            .ToDictionary(t => t.Id, t => t.DependsOn!);

        var testFileMap = testCases.ToDictionary(t => t.Id, t => t.FilePath);

        foreach (var testId in dependencyMap.Keys)
        {
            var visited = new HashSet<string>();
            var current = testId;

            while (!string.IsNullOrWhiteSpace(current) && dependencyMap.ContainsKey(current))
            {
                if (visited.Contains(current))
                {
                    // Circular dependency detected
                    var cycle = string.Join(" -> ", visited) + " -> " + current;
                    errors.Add(new ValidationError(
                        "CIRCULAR_DEPENDENCY",
                        $"Circular dependency detected: {cycle}",
                        testFileMap.GetValueOrDefault(testId, "unknown"),
                        testId));
                    break;
                }

                visited.Add(current);
                current = dependencyMap.GetValueOrDefault(current);
            }
        }

        return errors;
    }
}
