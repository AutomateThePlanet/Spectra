using System.Text.Json;
using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Coverage;

/// <summary>
/// Integration tests for the complete coverage analysis pipeline.
/// </summary>
public class CoverageAnalysisTests : IAsyncLifetime
{
    private string _tempDir = null!;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.Delay(50);
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task FullPipeline_ScanReconcileCalculate_ProducesAccurateReport()
    {
        // Arrange - Create test structure
        var testsDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testsDir);

        // Create automation file with test references
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "LoginTests.cs"),
            """
            using Xunit;

            public class LoginTests
            {
                [TestCase("TC-001")]
                public void Test_ValidLogin()
                {
                }

                [TestCase("TC-002")]
                public void Test_InvalidPassword()
                {
                }
            }
            """);

        // Create suite index
        var suiteIndex = new MetadataIndex
        {
            Suite = "auth",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001",
                    File = "TC-001.md",
                    Title = "Valid Login",
                    Priority = "P1",
                    Component = "Login",
                    SourceRefs = ["tests/LoginTests.cs"]
                },
                new TestIndexEntry
                {
                    Id = "TC-002",
                    File = "TC-002.md",
                    Title = "Invalid Password",
                    Priority = "P1",
                    Component = "Login",
                    SourceRefs = ["tests/LoginTests.cs"]
                },
                new TestIndexEntry
                {
                    Id = "TC-003",
                    File = "TC-003.md",
                    Title = "Manual Test",
                    Priority = "P2",
                    Component = "Logout",
                    SourceRefs = []
                }
            ]
        };

        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = suiteIndex
        };

        // Act - Run the pipeline
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        // Assert - Verify results
        Assert.Equal(3, report.Summary.TotalTests);
        Assert.Equal(2, report.Summary.Automated);
        Assert.Equal(1, report.Summary.ManualOnly);
        Assert.Equal(66.67m, report.Summary.CoveragePercentage);

        // Check suite coverage
        Assert.Single(report.BySuite);
        Assert.Equal(66.67m, report.BySuite[0].CoveragePercentage);

        // Check component coverage
        Assert.Equal(2, report.ByComponent.Count);

        var loginComponent = report.ByComponent.First(c => c.Component == "Login");
        Assert.Equal(100m, loginComponent.CoveragePercentage);

        var logoutComponent = report.ByComponent.First(c => c.Component == "Logout");
        Assert.Equal(0m, logoutComponent.CoveragePercentage);

        // Check unlinked tests
        Assert.Single(report.UnlinkedTests);
        Assert.Equal("TC-003", report.UnlinkedTests[0].TestId);
    }

    [Fact]
    public async Task FullPipeline_OrphanedAutomation_DetectsCorrectly()
    {
        // Arrange
        var testsDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testsDir);

        // Create automation file referencing non-existent test
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "OrphanTests.cs"),
            """
            [TestCase("TC-999")]
            public void Test_Orphan() { }
            """);

        var suiteIndexes = new Dictionary<string, MetadataIndex>();

        // Act
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Single(report.OrphanedAutomation);
        Assert.Contains("TC-999", report.OrphanedAutomation[0].ReferencedIds);
    }

    [Fact]
    public async Task FullPipeline_BrokenLink_DetectsCorrectly()
    {
        // Arrange
        var suiteIndex = new MetadataIndex
        {
            Suite = "auth",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001",
                    File = "TC-001.md",
                    Title = "Test with missing file",
                    Priority = "P1",
                    SourceRefs = ["tests/NonExistent.cs"]
                }
            ]
        };

        var suiteIndexes = new Dictionary<string, MetadataIndex> { ["auth"] = suiteIndex };

        // Act
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Single(report.BrokenLinks);
        Assert.Equal("TC-001", report.BrokenLinks[0].TestId);
        Assert.Equal("tests/NonExistent.cs", report.BrokenLinks[0].AutomatedBy);
    }

    [Fact]
    public async Task FullPipeline_MultipleSuites_AggregatesCorrectly()
    {
        // Arrange
        var testsDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testsDir);

        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "AuthTests.cs"),
            """[TestCase("TC-001")] public void Test() { }""");

        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "BillingTests.cs"),
            """[TestCase("TC-002")] public void Test() { }""");

        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Auth", Priority = "P1", SourceRefs = ["tests/AuthTests.cs"] },
                    new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "Manual", Priority = "P2", SourceRefs = [] }
                ]
            },
            ["billing"] = new()
            {
                Suite = "billing",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Billing", Priority = "P1", SourceRefs = ["tests/BillingTests.cs"] }
                ]
            }
        };

        // Act
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(3, report.Summary.TotalTests);
        Assert.Equal(2, report.Summary.Automated);
        Assert.Equal(66.67m, report.Summary.CoveragePercentage);

        Assert.Equal(2, report.BySuite.Count);

        var authSuite = report.BySuite.First(s => s.Suite == "auth");
        Assert.Equal(50m, authSuite.CoveragePercentage);

        var billingSuite = report.BySuite.First(s => s.Suite == "billing");
        Assert.Equal(100m, billingSuite.CoveragePercentage);
    }

    [Fact]
    public async Task FullPipeline_BidirectionalMismatch_DetectsCorrectly()
    {
        // Arrange
        var testsDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testsDir);

        // Automation file references TC-002, but test points to different file
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "WrongFile.cs"),
            """[TestCase("TC-001")] public void Test() { }""");

        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Mismatched Test",
                        Priority = "P1",
                        SourceRefs = ["tests/OtherFile.cs"] // Points to different file
                    }
                ]
            }
        };

        // Act
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.NotEmpty(report.Mismatches);
    }

    [Fact]
    public async Task FullPipeline_JsonSerialization_ProducesValidJson()
    {
        // Arrange
        var testsDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testsDir);

        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "Test.cs"),
            """[TestCase("TC-001")] public void Test() { }""");

        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests =
                [
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test", Priority = "P1", SourceRefs = ["tests/Test.cs"] }
                ]
            }
        };

        // Act
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - Verify it's valid JSON that can be deserialized
        Assert.NotEmpty(json);
        Assert.Contains("summary", json);
        Assert.Contains("by_suite", json);  // snake_case from JsonPropertyName
        Assert.Contains("by_component", json);
    }

    [Fact]
    public async Task FullPipeline_EmptyRepository_HandlesGracefully()
    {
        // Arrange - Empty repo
        var suiteIndexes = new Dictionary<string, MetadataIndex>();

        // Act
        var scanner = new AutomationScanner(_tempDir);
        var automationFiles = await scanner.ScanAsync();

        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        var calculator = new CoverageCalculator();
        var report = calculator.Calculate(suiteIndexes, reconciliation);

        // Assert
        Assert.Equal(0, report.Summary.TotalTests);
        Assert.Equal(0m, report.Summary.CoveragePercentage);
        Assert.Empty(report.BySuite);
        Assert.Empty(report.ByComponent);
    }
}
