using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.Core.Tests.Validation;

public class DuplicateDetectorTests
{
    private readonly DuplicateDetector _detector;

    public DuplicateDetectorTests()
    {
        _detector = new DuplicateDetector(threshold: 0.6);
    }

    [Fact]
    public void FindDuplicates_ExactTitleMatch_ReturnsDuplicate()
    {
        var candidate = CreateTestCase("TC-001", "Checkout with valid credit card");
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout with valid credit card")
        };

        var duplicates = _detector.FindDuplicates(candidate, existing);

        Assert.Single(duplicates);
        Assert.Equal("TC-002", duplicates[0].MatchedTestId);
        Assert.Equal(1.0, duplicates[0].TitleSimilarity, 2);
    }

    [Fact]
    public void FindDuplicates_SimilarTitles_ReturnsDuplicate()
    {
        var candidate = CreateTestCase("TC-001", "Checkout with valid Visa card");
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout with valid credit card")
        };

        var duplicates = _detector.FindDuplicates(candidate, existing);

        Assert.Single(duplicates);
        Assert.True(duplicates[0].Similarity >= 0.6);
    }

    [Fact]
    public void FindDuplicates_DifferentTitles_ReturnsEmpty()
    {
        var candidate = CreateTestCase("TC-001", "Login with username and password");
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout with valid credit card")
        };

        var duplicates = _detector.FindDuplicates(candidate, existing);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void FindDuplicates_SameSteps_IncreasesSimilarity()
    {
        var steps = new[] { "Navigate to checkout", "Enter card details", "Click pay" };
        var candidate = CreateTestCase("TC-001", "Payment test A", steps);
        var existing = new[]
        {
            CreateTestCase("TC-002", "Payment test B", steps)
        };

        var duplicates = _detector.FindDuplicates(candidate, existing);

        Assert.Single(duplicates);
        Assert.Equal(1.0, duplicates[0].StepSimilarity, 2);
    }

    [Fact]
    public void FindDuplicates_PartialStepOverlap_ReturnsPartialSimilarity()
    {
        var steps1 = new[] { "Navigate to checkout", "Enter card details", "Click pay" };
        var steps2 = new[] { "Navigate to checkout", "Enter card details", "Verify confirmation" };

        // Use identical titles so only step similarity varies
        var candidate = CreateTestCase("TC-001", "Checkout payment test", steps1);
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout payment test", steps2)
        };

        var duplicates = _detector.FindDuplicates(candidate, existing);

        Assert.Single(duplicates);
        // Steps: 2 common out of 4 unique = 0.5 Jaccard similarity
        Assert.True(duplicates[0].StepSimilarity >= 0.4 && duplicates[0].StepSimilarity <= 0.6);
    }

    [Fact]
    public void FindDuplicates_SkipsSelfComparison()
    {
        var test = CreateTestCase("TC-001", "Test checkout");
        var existing = new[] { test };

        var duplicates = _detector.FindDuplicates(test, existing);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void FindDuplicates_MultipleMatches_ReturnsAllSortedByScore()
    {
        var candidate = CreateTestCase("TC-001", "Checkout with credit card");
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout with credit card"), // Exact match
            CreateTestCase("TC-003", "Checkout with debit card"),  // Similar
            CreateTestCase("TC-004", "Login test")                 // Different
        };

        var duplicates = _detector.FindDuplicates(candidate, existing);

        Assert.Equal(2, duplicates.Count);
        Assert.Equal("TC-002", duplicates[0].MatchedTestId);
        Assert.True(duplicates[0].Similarity > duplicates[1].Similarity);
    }

    [Fact]
    public void IsPotentialDuplicate_WithDuplicate_ReturnsTrue()
    {
        var candidate = CreateTestCase("TC-001", "Checkout with credit card");
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout with credit card")
        };

        var isDuplicate = _detector.IsPotentialDuplicate(candidate, existing);

        Assert.True(isDuplicate);
    }

    [Fact]
    public void IsPotentialDuplicate_NoDuplicate_ReturnsFalse()
    {
        var candidate = CreateTestCase("TC-001", "Checkout test", ["Add to cart", "Go to checkout"]);
        var existing = new[]
        {
            CreateTestCase("TC-002", "Login test", ["Enter username", "Click login"])
        };

        var isDuplicate = _detector.IsPotentialDuplicate(candidate, existing);

        Assert.False(isDuplicate);
    }

    [Fact]
    public void FindAllDuplicatePairs_FindsDuplicatesInCollection()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Checkout with credit card"),
            CreateTestCase("TC-002", "Checkout with credit card"),
            CreateTestCase("TC-003", "Login test")
        };

        var pairs = _detector.FindAllDuplicatePairs(tests);

        Assert.Single(pairs);
        Assert.Contains(pairs, p =>
            (p.TestId1 == "TC-001" && p.TestId2 == "TC-002") ||
            (p.TestId1 == "TC-002" && p.TestId2 == "TC-001"));
    }

    [Fact]
    public void FindAllDuplicatePairs_NoDuplicates_ReturnsEmpty()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Checkout test", ["Add item to cart", "Go to checkout"]),
            CreateTestCase("TC-002", "Login test", ["Enter username", "Click login button"]),
            CreateTestCase("TC-003", "Signup test", ["Fill registration form", "Submit registration"])
        };

        var pairs = _detector.FindAllDuplicatePairs(tests);

        Assert.Empty(pairs);
    }

    [Fact]
    public void Constructor_ThresholdOutOfRange_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuplicateDetector(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuplicateDetector(1.1));
    }

    [Fact]
    public void CalculateTitleSimilarity_CaseInsensitive()
    {
        var similarity1 = _detector.CalculateTitleSimilarity("CHECKOUT TEST", "checkout test");

        Assert.Equal(1.0, similarity1, 2);
    }

    [Fact]
    public void CalculateTitleSimilarity_IgnoresPunctuation()
    {
        var similarity = _detector.CalculateTitleSimilarity(
            "Checkout: with credit-card!",
            "Checkout with credit card");

        Assert.True(similarity > 0.9);
    }

    [Fact]
    public void CustomThreshold_AffectsResults()
    {
        var strictDetector = new DuplicateDetector(threshold: 0.9);
        var lenientDetector = new DuplicateDetector(threshold: 0.3);

        var candidate = CreateTestCase("TC-001", "Checkout with Visa");
        var existing = new[]
        {
            CreateTestCase("TC-002", "Checkout with credit card")
        };

        var strictDuplicates = strictDetector.FindDuplicates(candidate, existing);
        var lenientDuplicates = lenientDetector.FindDuplicates(candidate, existing);

        Assert.Empty(strictDuplicates);
        Assert.NotEmpty(lenientDuplicates);
    }

    private static TestCase CreateTestCase(
        string id,
        string title,
        IReadOnlyList<string>? steps = null)
    {
        return new TestCase
        {
            Id = id,
            Title = title,
            Priority = Priority.Medium,
            Steps = steps ?? ["Step 1", "Step 2"],
            ExpectedResult = "Expected result",
            FilePath = $"{id}.md"
        };
    }
}
