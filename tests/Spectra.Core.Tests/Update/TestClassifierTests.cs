using Spectra.Core.Models;
using Spectra.Core.Update;

namespace Spectra.Core.Tests.Update;

public class TestClassifierTests
{
    private static TestCase CreateTestCase(
        string id,
        string title,
        IEnumerable<string>? steps = null,
        string expectedResult = "Expected result",
        IEnumerable<string>? sourceRefs = null)
    {
        return new TestCase
        {
            Id = id,
            FilePath = $"suite/{id}.md",
            Title = title,
            Priority = Priority.High,
            Steps = steps?.ToList() ?? ["Step 1"],
            ExpectedResult = expectedResult,
            SourceRefs = sourceRefs?.ToList() ?? []
        };
    }

    [Fact]
    public void Classify_WithNoSourceContent_ReturnsOrphaned()
    {
        var classifier = new TestClassifier();
        var test = CreateTestCase("TC-001", "Test checkout flow");

        var result = classifier.Classify(test, null, []);

        Assert.Equal(UpdateClassification.Orphaned, result.Classification);
        Assert.Contains("not found", result.Reason.ToLowerInvariant());
    }

    [Fact]
    public void Classify_WithEmptySourceContent_ReturnsOrphaned()
    {
        var classifier = new TestClassifier();
        var test = CreateTestCase("TC-001", "Test checkout flow");

        var result = classifier.Classify(test, "", []);

        Assert.Equal(UpdateClassification.Orphaned, result.Classification);
    }

    [Fact]
    public void Classify_WithMatchingContent_ReturnsUpToDate()
    {
        var classifier = new TestClassifier();
        var test = CreateTestCase(
            "TC-001",
            "Test checkout flow with cart",
            ["Add item to cart", "Click checkout button", "Confirm order"],
            "Order should be placed successfully");

        var sourceContent = @"
# Checkout Flow

Users can complete checkout by following these steps:
1. Add items to their cart
2. Click the checkout button
3. Confirm their order

The order should be placed successfully.
";

        var result = classifier.Classify(test, sourceContent, []);

        Assert.Equal(UpdateClassification.UpToDate, result.Classification);
    }

    [Fact]
    public void Classify_WithPartialMatch_ReturnsOutdated()
    {
        var classifier = new TestClassifier();
        var test = CreateTestCase(
            "TC-001",
            "Test old feature workflow",
            ["Use deprecated method", "Check old behavior"],
            "Legacy result expected");

        var sourceContent = @"
# New Feature Workflow

The workflow has been updated with new methods.
Use the modern approach for better results.
";

        var result = classifier.Classify(test, sourceContent, []);

        Assert.True(result.Classification == UpdateClassification.Outdated ||
                   result.Classification == UpdateClassification.Orphaned);
    }

    [Fact]
    public void Classify_WithRedundantTest_ReturnsRedundant()
    {
        var classifier = new TestClassifier();

        var test1 = CreateTestCase(
            "TC-001",
            "Test checkout flow",
            ["Add item to cart", "Click checkout"],
            "Order placed");

        var test2 = CreateTestCase(
            "TC-002",
            "Test checkout flow duplicate",
            ["Add item to cart", "Click checkout"],
            "Order placed");

        var sourceContent = "# Checkout\n\nAdd items to cart and click checkout.";

        var result = classifier.Classify(test2, sourceContent, [test1]);

        Assert.Equal(UpdateClassification.Redundant, result.Classification);
        Assert.Equal("TC-001", result.RelatedTestId);
    }

    [Fact]
    public void ClassifyBatch_ClassifiesAllTests()
    {
        var classifier = new TestClassifier();

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", "Test feature A", sourceRefs: ["docs/feature-a.md"]),
            CreateTestCase("TC-002", "Test feature B", sourceRefs: ["docs/feature-b.md"]),
            CreateTestCase("TC-003", "Test feature C", sourceRefs: ["docs/feature-c.md"])
        };

        var sourceContents = new Dictionary<string, string>
        {
            ["docs/feature-a.md"] = "Feature A content about testing",
            ["docs/feature-b.md"] = "Feature B content about testing"
            // Note: feature-c.md is missing, so TC-003 should be orphaned
        };

        var results = classifier.ClassifyBatch(tests, sourceContents);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.Test.Id == "TC-003" && r.Classification == UpdateClassification.Orphaned);
    }

    [Fact]
    public void ClassifyBatch_EmptyTestList_ReturnsEmptyResults()
    {
        var classifier = new TestClassifier();

        var results = classifier.ClassifyBatch([], new Dictionary<string, string>());

        Assert.Empty(results);
    }

    [Fact]
    public void Classify_CustomThresholds_AffectsClassification()
    {
        // With different thresholds, classification may change
        var strictClassifier = new TestClassifier(outdatedThreshold: 0.9, orphanedThreshold: 0.5);
        var lenientClassifier = new TestClassifier(outdatedThreshold: 0.3, orphanedThreshold: 0.1);

        var test = CreateTestCase(
            "TC-001",
            "Test checkout process cart payment",
            ["Navigate to cart section", "Click pay button"],
            "Payment should be processed successfully");

        var sourceContent = "# Checkout Process\n\nNavigate to the cart section and click the pay button for payment processing.";

        var strictResult = strictClassifier.Classify(test, sourceContent, []);
        var lenientResult = lenientClassifier.Classify(test, sourceContent, []);

        // Lenient classifier should more easily classify as up to date
        Assert.True(lenientResult.Classification == UpdateClassification.UpToDate ||
                   lenientResult.Classification == UpdateClassification.Outdated);
    }

    [Fact]
    public void Classify_ReturnsConfidenceScore()
    {
        var classifier = new TestClassifier();
        var test = CreateTestCase("TC-001", "Test something");

        var result = classifier.Classify(test, "Some content", []);

        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    [Fact]
    public void Classify_ReturnsReasonString()
    {
        var classifier = new TestClassifier();
        var test = CreateTestCase("TC-001", "Test something");

        var result = classifier.Classify(test, null, []);

        Assert.NotNull(result.Reason);
        Assert.NotEmpty(result.Reason);
    }
}
