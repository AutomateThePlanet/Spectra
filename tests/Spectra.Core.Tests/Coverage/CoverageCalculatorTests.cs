using Spectra.Core.Coverage;
using Spectra.Core.Models;
using CoverageModels = Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Coverage;

public class CoverageCalculatorTests
{
    private readonly CoverageCalculator _calculator = new();

    [Fact]
    public void Calculate_EmptyInputs_ReturnsZeroCoverage()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>();
        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(0, result.Summary.TotalTests);
        Assert.Equal(0, result.Summary.Automated);
        Assert.Equal(0m, result.Summary.CoveragePercentage);
    }

    [Fact]
    public void Calculate_AllTestsAutomated_Returns100Percent()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", SourceRefs = [] }
                ]
            }
        };

        var validLinks = new List<CoverageModels.CoverageLink>
        {
            new() { Source = "TC-001", Target = "tests/T1.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid },
            new() { Source = "TC-002", Target = "tests/T2.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid }
        };

        var reconciliation = CreateReconciliation(validLinks, []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(2, result.Summary.TotalTests);
        Assert.Equal(2, result.Summary.Automated);
        Assert.Equal(0, result.Summary.ManualOnly);
        Assert.Equal(100m, result.Summary.CoveragePercentage);
    }

    [Fact]
    public void Calculate_NoTestsAutomated_Returns0Percent()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", SourceRefs = [] }
                ]
            }
        };

        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(2, result.Summary.TotalTests);
        Assert.Equal(0, result.Summary.Automated);
        Assert.Equal(2, result.Summary.ManualOnly);
        Assert.Equal(0m, result.Summary.CoveragePercentage);
    }

    [Fact]
    public void Calculate_PartialCoverage_ReturnsCorrectPercentage()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "Test 3", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-004", File = "TC-004.md", Title = "Test 4", Priority = "P1", SourceRefs = [] }
                ]
            }
        };

        var validLinks = new List<CoverageModels.CoverageLink>
        {
            new() { Source = "TC-001", Target = "tests/T1.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid }
        };

        var reconciliation = CreateReconciliation(validLinks, []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(4, result.Summary.TotalTests);
        Assert.Equal(1, result.Summary.Automated);
        Assert.Equal(3, result.Summary.ManualOnly);
        Assert.Equal(25m, result.Summary.CoveragePercentage);
    }

    [Fact]
    public void Calculate_BySuite_ReturnsPerSuiteStatistics()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Auth Test 1", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Auth Test 2", Priority = "P1", SourceRefs = [] }
                ]
            },
            ["billing"] = new()
            {
                Suite = "billing",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "Billing Test 1", Priority = "P1", SourceRefs = [] }
                ]
            }
        };

        var validLinks = new List<CoverageModels.CoverageLink>
        {
            new() { Source = "TC-001", Target = "tests/T1.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid },
            new() { Source = "TC-003", Target = "tests/T3.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid }
        };

        var reconciliation = CreateReconciliation(validLinks, []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(2, result.BySuite.Count);

        var authSuite = result.BySuite.First(s => s.Suite == "auth");
        Assert.Equal(2, authSuite.Total);
        Assert.Equal(1, authSuite.Automated);
        Assert.Equal(50m, authSuite.CoveragePercentage);

        var billingSuite = result.BySuite.First(s => s.Suite == "billing");
        Assert.Equal(1, billingSuite.Total);
        Assert.Equal(1, billingSuite.Automated);
        Assert.Equal(100m, billingSuite.CoveragePercentage);
    }

    [Fact]
    public void Calculate_ByComponent_ReturnsPerComponentStatistics()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", Component = "Login", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", Component = "Login", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "Test 3", Priority = "P1", Component = "Logout", SourceRefs = [] }
                ]
            }
        };

        var validLinks = new List<CoverageModels.CoverageLink>
        {
            new() { Source = "TC-001", Target = "tests/T1.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid },
            new() { Source = "TC-002", Target = "tests/T2.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid }
        };

        var reconciliation = CreateReconciliation(validLinks, []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(2, result.ByComponent.Count);

        var loginComponent = result.ByComponent.First(c => c.Component == "Login");
        Assert.Equal(2, loginComponent.Total);
        Assert.Equal(2, loginComponent.Automated);
        Assert.Equal(100m, loginComponent.CoveragePercentage);

        var logoutComponent = result.ByComponent.First(c => c.Component == "Logout");
        Assert.Equal(1, logoutComponent.Total);
        Assert.Equal(0, logoutComponent.Automated);
        Assert.Equal(0m, logoutComponent.CoveragePercentage);
    }

    [Fact]
    public void Calculate_ExcludesTestsWithNullComponent()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", Component = "Login", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", Component = null, SourceRefs = [] }
                ]
            }
        };

        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Single(result.ByComponent);
        Assert.Equal("Login", result.ByComponent[0].Component);
    }

    [Fact]
    public void Calculate_IncludesIssuesFromReconciliation()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>();

        var unlinkedTests = new List<CoverageModels.UnlinkedTest>
        {
            new() { TestId = "TC-001", Suite = "auth", Title = "Unlinked", Priority = "P1" }
        };

        var orphanedAutomation = new List<CoverageModels.OrphanedAutomation>
        {
            new() { File = "tests/Orphan.cs", ReferencedIds = ["TC-999"], LineNumbers = [10] }
        };

        var brokenLinks = new List<CoverageModels.BrokenLink>
        {
            new() { TestId = "TC-002", AutomatedBy = "missing.cs", Reason = "File not found" }
        };

        var mismatches = new List<CoverageModels.LinkMismatch>
        {
            new() { TestId = "TC-003", TestAutomatedBy = "a.cs", AutomationFile = "b.cs", Issue = "Mismatch" }
        };

        var reconciliation = new ReconciliationResult(
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<string>>(),
            [],
            unlinkedTests,
            orphanedAutomation,
            brokenLinks,
            mismatches);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Single(result.UnlinkedTests);
        Assert.Single(result.OrphanedAutomation);
        Assert.Single(result.BrokenLinks);
        Assert.Single(result.Mismatches);
        Assert.Equal(1, result.Summary.BrokenLinks);
        Assert.Equal(1, result.Summary.OrphanedAutomation);
        Assert.Equal(1, result.Summary.Mismatches);
    }

    [Fact]
    public void Calculate_RoundsPercentageToTwoDecimals()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "Test 3", Priority = "P1", SourceRefs = [] }
                ]
            }
        };

        var validLinks = new List<CoverageModels.CoverageLink>
        {
            new() { Source = "TC-001", Target = "tests/T1.cs", Type = CoverageModels.LinkType.TestToAutomation, Status = CoverageModels.LinkStatus.Valid }
        };

        var reconciliation = CreateReconciliation(validLinks, []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(33.33m, result.Summary.CoveragePercentage); // 1/3 rounded
    }

    [Fact]
    public void Calculate_SortsBySuiteAlphabetically()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["z-suite"] = new() { Suite = "z-suite", GeneratedAt = DateTime.UtcNow, Tests = [new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "T", Priority = "P1", SourceRefs = [] }] },
            ["a-suite"] = new() { Suite = "a-suite", GeneratedAt = DateTime.UtcNow, Tests = [new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "T", Priority = "P1", SourceRefs = [] }] },
            ["m-suite"] = new() { Suite = "m-suite", GeneratedAt = DateTime.UtcNow, Tests = [new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "T", Priority = "P1", SourceRefs = [] }] }
        };

        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal("a-suite", result.BySuite[0].Suite);
        Assert.Equal("m-suite", result.BySuite[1].Suite);
        Assert.Equal("z-suite", result.BySuite[2].Suite);
    }

    [Fact]
    public void Calculate_SortsByComponentAlphabetically()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "T", Priority = "P1", Component = "Zebra", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "T", Priority = "P1", Component = "Apple", SourceRefs = [] }
                ]
            }
        };

        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal("Apple", result.ByComponent[0].Component);
        Assert.Equal("Zebra", result.ByComponent[1].Component);
    }

    [Fact]
    public void Calculate_SetsGeneratedAtTimestamp()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>();
        var reconciliation = CreateReconciliation([], []);
        var before = DateTime.UtcNow;

        // Act
        var result = _calculator.Calculate(suiteIndexes, reconciliation);
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(result.GeneratedAt, before, after);
    }

    [Fact]
    public void CalculateSuiteCoverage_EmptySuite_ReturnsZero()
    {
        // Arrange
        var suiteIndex = new MetadataIndex
        {
            Suite = "empty",
            GeneratedAt = DateTime.UtcNow,
            Tests = []
        };
        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.CalculateSuiteCoverage(suiteIndex, reconciliation);

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateComponentCoverage_EmptyTests_ReturnsZero()
    {
        // Arrange
        var tests = Array.Empty<TestIndexEntry>();
        var reconciliation = CreateReconciliation([], []);

        // Act
        var result = _calculator.CalculateComponentCoverage(tests, reconciliation);

        // Assert
        Assert.Equal(0m, result);
    }

    private static ReconciliationResult CreateReconciliation(
        IReadOnlyList<CoverageModels.CoverageLink> validLinks,
        IReadOnlyList<CoverageModels.UnlinkedTest> unlinkedTests)
    {
        return new ReconciliationResult(
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<string>>(),
            validLinks,
            unlinkedTests,
            [],
            [],
            []);
    }
}
