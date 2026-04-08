using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Update;

/// <summary>
/// Classifies tests based on their relationship to source documentation.
/// </summary>
public sealed class TestClassifier
{
    private readonly double _outdatedThreshold;
    private readonly double _orphanedThreshold;

    /// <summary>
    /// Creates a new TestClassifier.
    /// </summary>
    /// <param name="outdatedThreshold">Similarity threshold below which a test is considered outdated (default: 0.7).</param>
    /// <param name="orphanedThreshold">Similarity threshold below which a test is considered orphaned (default: 0.3).</param>
    public TestClassifier(double outdatedThreshold = 0.7, double orphanedThreshold = 0.3)
    {
        _outdatedThreshold = outdatedThreshold;
        _orphanedThreshold = orphanedThreshold;
    }

    /// <summary>
    /// Classifies a single test against its source documentation.
    /// </summary>
    public ClassificationResult Classify(
        TestCase test,
        string? sourceContent,
        IReadOnlyList<TestCase> otherTests)
    {
        return Classify(test, sourceContent, otherTests, null);
    }

    /// <summary>
    /// Classifies a single test against its source documentation and acceptance criteria.
    /// </summary>
    public ClassificationResult Classify(
        TestCase test,
        string? sourceContent,
        IReadOnlyList<TestCase> otherTests,
        IReadOnlyList<AcceptanceCriterion>? criteria)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(otherTests);

        // Check criteria-based classification first (if criteria data is available)
        if (criteria is not null && test.Criteria.Count > 0)
        {
            var criteriaById = criteria.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var criterionId in test.Criteria)
            {
                if (!criteriaById.TryGetValue(criterionId, out var criterion))
                {
                    // Criterion ID no longer exists
                    return new ClassificationResult
                    {
                        Test = test,
                        Classification = UpdateClassification.Orphaned,
                        Confidence = 1.0,
                        Reason = $"Linked criterion {criterionId} no longer exists"
                    };
                }

                // Check if criterion text has meaningfully changed
                // We compare the criterion text against the test's scenario_from_doc or title
                // to detect when the criterion was updated but the test was not
                if (!string.IsNullOrEmpty(criterion.Text) && !string.IsNullOrEmpty(test.ScenarioFromDoc))
                {
                    var similarity = CalculateTextSimilarity(criterion.Text, test.ScenarioFromDoc);
                    if (similarity < _outdatedThreshold)
                    {
                        return new ClassificationResult
                        {
                            Test = test,
                            Classification = UpdateClassification.Outdated,
                            Confidence = _outdatedThreshold - similarity,
                            Reason = $"Linked criterion {criterionId} text changed"
                        };
                    }
                }
            }
        }

        // Check for orphaned (no source content or source not found)
        if (string.IsNullOrWhiteSpace(sourceContent))
        {
            return new ClassificationResult
            {
                Test = test,
                Classification = UpdateClassification.Orphaned,
                Confidence = 1.0,
                Reason = "Source document not found or empty"
            };
        }

        // Check for redundancy against other tests
        var redundantWith = FindRedundantTest(test, otherTests);
        if (redundantWith is not null)
        {
            return new ClassificationResult
            {
                Test = test,
                Classification = UpdateClassification.Redundant,
                Confidence = 0.8,
                Reason = $"Duplicates test {redundantWith.Id}",
                RelatedTestId = redundantWith.Id
            };
        }

        // Calculate relevance to source content
        var relevance = CalculateRelevance(test, sourceContent);

        if (relevance < _orphanedThreshold)
        {
            return new ClassificationResult
            {
                Test = test,
                Classification = UpdateClassification.Orphaned,
                Confidence = 1.0 - relevance,
                Reason = "Test content no longer matches source documentation"
            };
        }

        if (relevance < _outdatedThreshold)
        {
            return new ClassificationResult
            {
                Test = test,
                Classification = UpdateClassification.Outdated,
                Confidence = _outdatedThreshold - relevance,
                Reason = "Test may need updates to match current documentation"
            };
        }

        return new ClassificationResult
        {
            Test = test,
            Classification = UpdateClassification.UpToDate,
            Confidence = relevance,
            Reason = "Test matches current documentation"
        };
    }

    /// <summary>
    /// Classifies multiple tests in batch.
    /// </summary>
    public IReadOnlyList<ClassificationResult> ClassifyBatch(
        IReadOnlyList<TestCase> tests,
        IReadOnlyDictionary<string, string> sourceContents)
    {
        return ClassifyBatch(tests, sourceContents, null);
    }

    /// <summary>
    /// Classifies multiple tests in batch with acceptance criteria awareness.
    /// </summary>
    public IReadOnlyList<ClassificationResult> ClassifyBatch(
        IReadOnlyList<TestCase> tests,
        IReadOnlyDictionary<string, string> sourceContents,
        IReadOnlyList<AcceptanceCriterion>? criteria)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(sourceContents);

        var results = new List<ClassificationResult>();

        foreach (var test in tests)
        {
            // Find source content for this test
            string? sourceContent = null;
            foreach (var sourceRef in test.SourceRefs)
            {
                if (sourceContents.TryGetValue(sourceRef, out var content))
                {
                    sourceContent = content;
                    break;
                }
            }

            var otherTests = tests.Where(t => t.Id != test.Id).ToList();
            results.Add(Classify(test, sourceContent, otherTests, criteria));
        }

        return results;
    }

    /// <summary>
    /// Finds tests that might be redundant with the given test.
    /// </summary>
    private TestCase? FindRedundantTest(TestCase test, IReadOnlyList<TestCase> otherTests)
    {
        foreach (var other in otherTests)
        {
            if (other.Id == test.Id)
            {
                continue;
            }

            var similarity = CalculateTestSimilarity(test, other);
            if (similarity > 0.8)
            {
                return other;
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates how relevant a test is to source content.
    /// </summary>
    private static double CalculateRelevance(TestCase test, string sourceContent)
    {
        var sourceLower = sourceContent.ToLowerInvariant();
        var matches = 0;
        var total = 0;

        // Check title words
        var titleWords = test.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in titleWords)
        {
            if (word.Length > 3 && sourceLower.Contains(word))
            {
                matches++;
            }
            total++;
        }

        // Check steps
        foreach (var step in test.Steps)
        {
            var stepWords = step.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in stepWords)
            {
                if (word.Length > 3 && sourceLower.Contains(word))
                {
                    matches++;
                }
                total++;
            }
        }

        // Check expected result
        var expectedWords = test.ExpectedResult.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in expectedWords)
        {
            if (word.Length > 3 && sourceLower.Contains(word))
            {
                matches++;
            }
            total++;
        }

        return total > 0 ? (double)matches / total : 0;
    }

    /// <summary>
    /// Calculates word-overlap similarity between two text strings using Jaccard similarity.
    /// </summary>
    private static double CalculateTextSimilarity(string textA, string textB)
    {
        var aWords = new HashSet<string>(
            textA.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3));
        var bWords = new HashSet<string>(
            textB.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3));

        if (aWords.Count == 0 && bWords.Count == 0) return 1.0;
        if (aWords.Count == 0 || bWords.Count == 0) return 0.0;

        var intersection = aWords.Intersect(bWords).Count();
        var union = aWords.Union(bWords).Count();

        return union > 0 ? (double)intersection / union : 0;
    }

    /// <summary>
    /// Calculates similarity between two tests.
    /// </summary>
    private static double CalculateTestSimilarity(TestCase a, TestCase b)
    {
        // Title similarity (Jaccard)
        var aWords = new HashSet<string>(a.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var bWords = new HashSet<string>(b.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var intersection = aWords.Intersect(bWords).Count();
        var union = aWords.Union(bWords).Count();

        var titleSimilarity = union > 0 ? (double)intersection / union : 0;

        // Steps similarity
        var aSteps = new HashSet<string>(a.Steps.Select(s => s.ToLowerInvariant()));
        var bSteps = new HashSet<string>(b.Steps.Select(s => s.ToLowerInvariant()));

        var stepsIntersection = aSteps.Intersect(bSteps).Count();
        var stepsUnion = aSteps.Union(bSteps).Count();

        var stepsSimilarity = stepsUnion > 0 ? (double)stepsIntersection / stepsUnion : 0;

        // Weighted combination
        return titleSimilarity * 0.4 + stepsSimilarity * 0.6;
    }
}

/// <summary>
/// Result of classifying a test.
/// </summary>
public sealed record ClassificationResult
{
    public required TestCase Test { get; init; }
    public required UpdateClassification Classification { get; init; }
    public required double Confidence { get; init; }
    public required string Reason { get; init; }
    public string? RelatedTestId { get; init; }
}
