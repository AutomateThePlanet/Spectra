using System.Collections.Concurrent;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Validation;

/// <summary>
/// Orchestrates all validation steps for test suites.
/// Optimized for large test suites (500+ files) with parallel processing.
/// </summary>
public sealed class ValidationOrchestrator
{
    private readonly TestValidator _testValidator;
    private readonly IdUniquenessValidator _idUniquenessValidator;
    private readonly DependsOnValidator _dependsOnValidator;
    private readonly IndexFreshnessValidator _indexFreshnessValidator;

    /// <summary>
    /// Threshold for enabling parallel processing.
    /// </summary>
    private const int ParallelThreshold = 50;

    /// <summary>
    /// Maximum degree of parallelism for validation operations.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    public ValidationOrchestrator(ValidationConfig? config = null)
    {
        _testValidator = new TestValidator(config);
        _idUniquenessValidator = new IdUniquenessValidator();
        _dependsOnValidator = new DependsOnValidator();
        _indexFreshnessValidator = new IndexFreshnessValidator();
    }

    /// <summary>
    /// Validates a single test suite.
    /// </summary>
    public ValidationResult ValidateSuite(TestSuite suite)
    {
        ArgumentNullException.ThrowIfNull(suite);

        var results = new List<ValidationResult>();

        // Validate individual test cases
        results.Add(_testValidator.ValidateAll(suite.Tests));

        // Validate ID uniqueness within suite
        results.Add(_idUniquenessValidator.ValidateSuite(suite.Name, suite.Tests));

        // Validate depends_on references
        results.Add(_dependsOnValidator.Validate(suite.Tests));

        // Validate index freshness if index exists
        if (suite.Index is not null)
        {
            results.Add(_indexFreshnessValidator.Validate(
                suite.Index,
                suite.Tests,
                suite.Path));
        }

        return ValidationResult.Combine(results);
    }

    /// <summary>
    /// Validates multiple test suites.
    /// Uses parallel processing for large suites (50+ suites).
    /// </summary>
    public ValidationResult ValidateAll(IEnumerable<TestSuite> suites)
    {
        ArgumentNullException.ThrowIfNull(suites);

        var suiteList = suites.ToList();

        // Use parallel validation for large numbers of suites
        var suiteResults = suiteList.Count >= ParallelThreshold
            ? ValidateSuitesParallel(suiteList)
            : ValidateSuitesSequential(suiteList);

        var results = new List<ValidationResult>(suiteResults);

        // Validate ID uniqueness across all suites
        var allTests = suiteList.SelectMany(s => s.Tests).ToList();
        results.Add(_idUniquenessValidator.Validate(allTests));

        return ValidationResult.Combine(results);
    }

    private List<ValidationResult> ValidateSuitesSequential(List<TestSuite> suites)
    {
        var results = new List<ValidationResult>();
        foreach (var suite in suites)
        {
            results.Add(ValidateSuite(suite));
        }
        return results;
    }

    private List<ValidationResult> ValidateSuitesParallel(List<TestSuite> suites)
    {
        var results = new ConcurrentBag<ValidationResult>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism };

        Parallel.ForEach(suites, options, suite =>
        {
            results.Add(ValidateSuite(suite));
        });

        return results.ToList();
    }

    /// <summary>
    /// Validates only test schemas (fast validation).
    /// </summary>
    public ValidationResult ValidateSchemaOnly(IEnumerable<TestCase> testCases)
    {
        return _testValidator.ValidateAll(testCases);
    }

    /// <summary>
    /// Checks if any index needs rebuilding.
    /// </summary>
    public bool AnyIndexNeedsRebuild(IEnumerable<TestSuite> suites)
    {
        foreach (var suite in suites)
        {
            var indexPath = Path.Combine(suite.Path, "_index.json");
            var testPaths = suite.Tests.Select(t => t.FilePath);

            if (_indexFreshnessValidator.NeedsRebuild(indexPath, testPaths))
            {
                return true;
            }
        }

        return false;
    }
}
