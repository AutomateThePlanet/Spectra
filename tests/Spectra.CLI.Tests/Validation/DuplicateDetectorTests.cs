using Spectra.CLI.Validation;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Validation;

public class DuplicateDetectorTests
{
    private readonly DuplicateDetector _detector = new(threshold: 0.8);

    [Fact]
    public void IdenticalTitles_ReturnMatch()
    {
        var tests = new List<TestCase>
        {
            CreateTest("TC-001", "Login with valid credentials")
        };

        var matches = _detector.FindDuplicates("Login with valid credentials", tests);
        Assert.Single(matches);
        Assert.Equal(1.0, matches[0].Similarity, precision: 2);
    }

    [Fact]
    public void VeryDifferentTitles_ReturnNoMatch()
    {
        var tests = new List<TestCase>
        {
            CreateTest("TC-001", "Login with valid credentials")
        };

        var matches = _detector.FindDuplicates("Payment timeout after 30 seconds", tests);
        Assert.Empty(matches);
    }

    [Fact]
    public void SimilarTitles_AboveThreshold_ReturnMatch()
    {
        var tests = new List<TestCase>
        {
            CreateTest("TC-001", "Login with valid user credentials")
        };

        var matches = _detector.FindDuplicates("Login with valid credentials", tests);
        Assert.Single(matches);
        Assert.True(matches[0].Similarity >= 0.8);
    }

    [Fact]
    public void CaseInsensitiveMatching()
    {
        var tests = new List<TestCase>
        {
            CreateTest("TC-001", "LOGIN WITH VALID CREDENTIALS")
        };

        var matches = _detector.FindDuplicates("login with valid credentials", tests);
        Assert.Single(matches);
    }

    [Fact]
    public void EmptyExistingTests_ReturnNoMatch()
    {
        var matches = _detector.FindDuplicates("Any title", []);
        Assert.Empty(matches);
    }

    [Fact]
    public void MultipleMatches_SortedBySimilarity()
    {
        var tests = new List<TestCase>
        {
            CreateTest("TC-001", "Login with valid credentials"),
            CreateTest("TC-002", "Login with invalid credentials"),
            CreateTest("TC-003", "Checkout with credit card")
        };

        var matches = _detector.FindDuplicates("Login with valid user credentials", tests);
        Assert.True(matches.Count >= 1);
        // First match should be most similar
        if (matches.Count > 1)
        {
            Assert.True(matches[0].Similarity >= matches[1].Similarity);
        }
    }

    [Fact]
    public void ComputeSimilarity_IdenticalStrings_Returns1()
    {
        Assert.Equal(1.0, DuplicateDetector.ComputeSimilarity("hello", "hello"));
    }

    [Fact]
    public void ComputeSimilarity_CompletelyDifferent_ReturnsLow()
    {
        var sim = DuplicateDetector.ComputeSimilarity("abc", "xyz");
        Assert.True(sim < 0.5);
    }

    [Fact]
    public void ComputeSimilarity_EmptyStrings_Returns1()
    {
        Assert.Equal(1.0, DuplicateDetector.ComputeSimilarity("", ""));
    }

    [Fact]
    public void ComputeSimilarity_OneEmpty_Returns0()
    {
        Assert.Equal(0.0, DuplicateDetector.ComputeSimilarity("hello", ""));
    }

    private static TestCase CreateTest(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Priority = Priority.Medium,
        Tags = [],
        Steps = [],
        ExpectedResult = "",
        FilePath = $"tests/{id}.md"
    };
}
