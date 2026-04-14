using Spectra.CLI.Agent.Analysis;

namespace Spectra.CLI.Tests.Agent.Analysis;

public class CoverageContextFormatterTests
{
    /// <summary>T012: Full mode includes titles, criteria, and source ref sections.</summary>
    [Fact]
    public void FullMode_IncludesTitlesAndCriteria()
    {
        var snapshot = new CoverageSnapshot
        {
            ExistingTestCount = 10,
            ExistingTestTitles = ["Login succeeds", "Login fails", "Password reset"],
            CoveredCriteriaIds = new HashSet<string> { "AC-001", "AC-002" },
            UncoveredCriteria =
            [
                new UncoveredCriterion("AC-003", "Forgotten password flow", "docs/auth.md", "high")
            ],
            CoveredSourceRefs = new HashSet<string> { "docs/auth.md" },
            UncoveredSourceRefs = ["docs/settings.md"],
            TotalCriteriaCount = 3
        };

        var output = CoverageContextFormatter.Format(snapshot);

        Assert.Contains("EXISTING COVERAGE", output);
        Assert.Contains("10 test cases", output);

        // Criteria coverage
        Assert.Contains("Acceptance Criteria Coverage (2/3)", output);
        Assert.Contains("AC-001", output);
        Assert.Contains("AC-002", output);

        // Uncovered criteria
        Assert.Contains("Uncovered Acceptance Criteria (1)", output);
        Assert.Contains("AC-003", output);
        Assert.Contains("Forgotten password flow", output);

        // Source refs
        Assert.Contains("Covered Documentation Sections", output);
        Assert.Contains("docs/auth.md", output);
        Assert.Contains("Uncovered Documentation Sections", output);
        Assert.Contains("docs/settings.md", output);

        // Test titles (full mode, <500 tests)
        Assert.Contains("Existing Test Titles", output);
        Assert.Contains("Login succeeds", output);
        Assert.Contains("Login fails", output);
        Assert.Contains("Password reset", output);

        // Analysis instructions
        Assert.Contains("ANALYSIS INSTRUCTIONS", output);
    }

    /// <summary>T013: Empty snapshot returns empty string.</summary>
    [Fact]
    public void EmptySnapshot_ReturnsEmpty()
    {
        var snapshot = new CoverageSnapshot();

        var output = CoverageContextFormatter.Format(snapshot);

        Assert.Equal("", output);
    }

    /// <summary>T014: Titles longer than 80 characters are truncated to 77 + "...".</summary>
    [Fact]
    public void TruncatesTitlesAt80Chars()
    {
        var longTitle = new string('A', 120);

        var snapshot = new CoverageSnapshot
        {
            ExistingTestCount = 1,
            ExistingTestTitles = [longTitle],
            TotalCriteriaCount = 0
        };

        var output = CoverageContextFormatter.Format(snapshot);

        // The truncated title should be 77 chars + "..." = 80 chars total
        var expectedTruncated = new string('A', 77) + "...";
        Assert.Contains(expectedTruncated, output);
        Assert.DoesNotContain(longTitle, output);

        // Verify via internal method directly
        var truncated = CoverageContextFormatter.TruncateTitle(longTitle);
        Assert.Equal(80, truncated.Length);
        Assert.EndsWith("...", truncated);
    }

    /// <summary>T033: Summary mode (>500 tests) omits title list, shows token message.</summary>
    [Fact]
    public void SummaryMode_Over500Tests()
    {
        var snapshot = new CoverageSnapshot
        {
            ExistingTestCount = 600,
            ExistingTestTitles = Enumerable.Range(1, 600).Select(i => $"Test {i}").ToList(),
            CoveredCriteriaIds = new HashSet<string> { "AC-001" },
            TotalCriteriaCount = 1
        };

        Assert.Equal(CoverageContextMode.Summary, snapshot.Mode);

        var output = CoverageContextFormatter.Format(snapshot);

        Assert.DoesNotContain("### Existing Test Titles", output);
        Assert.Contains("omitted to conserve tokens", output);
        Assert.Contains("600 existing tests", output);
    }

    /// <summary>T034: Summary mode still includes uncovered criteria section.</summary>
    [Fact]
    public void SummaryMode_StillIncludesUncovered()
    {
        var snapshot = new CoverageSnapshot
        {
            ExistingTestCount = 600,
            ExistingTestTitles = Enumerable.Range(1, 600).Select(i => $"Test {i}").ToList(),
            CoveredCriteriaIds = new HashSet<string> { "AC-001" },
            UncoveredCriteria =
            [
                new UncoveredCriterion("AC-002", "Uncovered edge case", "docs/api.md", "high"),
                new UncoveredCriterion("AC-003", "Another gap", null, "medium")
            ],
            TotalCriteriaCount = 3
        };

        var output = CoverageContextFormatter.Format(snapshot);

        // Uncovered criteria must still be present in summary mode
        Assert.Contains("Uncovered Acceptance Criteria (2)", output);
        Assert.Contains("AC-002", output);
        Assert.Contains("Uncovered edge case", output);
        Assert.Contains("AC-003", output);
        Assert.Contains("Another gap", output);
    }
}
