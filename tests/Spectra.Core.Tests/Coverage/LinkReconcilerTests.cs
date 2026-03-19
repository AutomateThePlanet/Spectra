using Spectra.Core.Coverage;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Coverage;

public class LinkReconcilerTests
{
    private readonly LinkReconciler _reconciler = new();

    [Fact]
    public void Reconcile_EmptyInputs_ReturnsEmptyResult()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>();
        var automationFiles = new Dictionary<string, AutomationFileInfo>();

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Empty(result.TestToAutomation);
        Assert.Empty(result.AutomationToTests);
        Assert.Empty(result.ValidLinks);
        Assert.Empty(result.UnlinkedTests);
        Assert.Empty(result.OrphanedAutomation);
        Assert.Empty(result.BrokenLinks);
        Assert.Empty(result.Mismatches);
    }

    [Fact]
    public void Reconcile_TestWithAutomationRef_BuildsTestToAutomationMap()
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
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Test Login",
                        Priority = "P1",
                        SourceRefs = ["tests/LoginTests.cs"]
                    }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/LoginTests.cs"] = new(
                "tests/LoginTests.cs",
                ["TC-001"],
                [new TestReference("TC-001", 10)])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.TestToAutomation);
        Assert.Equal("tests/LoginTests.cs", result.TestToAutomation["TC-001"]);
    }

    [Fact]
    public void Reconcile_AutomationReferencingTests_BuildsAutomationToTestsMap()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>();
        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/LoginTests.cs"] = new(
                "tests/LoginTests.cs",
                ["TC-001", "TC-002"],
                [
                    new TestReference("TC-001", 10),
                    new TestReference("TC-002", 20)
                ])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.AutomationToTests);
        Assert.Equal(2, result.AutomationToTests["tests/LoginTests.cs"].Count);
        Assert.Contains("TC-001", result.AutomationToTests["tests/LoginTests.cs"]);
        Assert.Contains("TC-002", result.AutomationToTests["tests/LoginTests.cs"]);
    }

    [Fact]
    public void Reconcile_UnlinkedTest_DetectsAsUnlinked()
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
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Test Without Automation",
                        Priority = "P1",
                        SourceRefs = [] // No automation reference
                    }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>();

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.UnlinkedTests);
        Assert.Equal("TC-001", result.UnlinkedTests[0].TestId);
        Assert.Equal("auth", result.UnlinkedTests[0].Suite);
    }

    [Fact]
    public void Reconcile_OrphanedAutomation_DetectsOrphans()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>();
        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/OrphanTests.cs"] = new(
                "tests/OrphanTests.cs",
                ["TC-999"], // References non-existent test
                [new TestReference("TC-999", 10)])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.OrphanedAutomation);
        Assert.Equal("tests/OrphanTests.cs", result.OrphanedAutomation[0].File);
        Assert.Contains("TC-999", result.OrphanedAutomation[0].ReferencedIds);
    }

    [Fact]
    public void Reconcile_BrokenLink_DetectsMissingFile()
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
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Test With Missing File",
                        Priority = "P1",
                        SourceRefs = ["tests/NonExistent.cs"] // File doesn't exist
                    }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>();

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.BrokenLinks);
        Assert.Equal("TC-001", result.BrokenLinks[0].TestId);
        Assert.Equal("tests/NonExistent.cs", result.BrokenLinks[0].AutomatedBy);
        Assert.Equal("File not found", result.BrokenLinks[0].Reason);
    }

    [Fact]
    public void Reconcile_Mismatch_TestPointsToAutomationButNotReferenced()
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
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Test With Mismatch",
                        Priority = "P1",
                        SourceRefs = ["tests/WrongTests.cs"]
                    }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/WrongTests.cs"] = new(
                "tests/WrongTests.cs",
                ["TC-002"], // Different test ID
                [new TestReference("TC-002", 10)])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.Mismatches);
        Assert.Equal("TC-001", result.Mismatches[0].TestId);
        Assert.Contains("doesn't reference test", result.Mismatches[0].Issue);
    }

    [Fact]
    public void Reconcile_Mismatch_AutomationReferencesTestPointingElsewhere()
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
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Test Points Elsewhere",
                        Priority = "P1",
                        SourceRefs = ["tests/OtherFile.cs"]
                    }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/LoginTests.cs"] = new(
                "tests/LoginTests.cs",
                ["TC-001"],
                [new TestReference("TC-001", 10)]),
            ["tests/OtherFile.cs"] = new(
                "tests/OtherFile.cs",
                [],
                [])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        var mismatch = result.Mismatches.FirstOrDefault(m =>
            m.TestId == "TC-001" && m.AutomationFile == "tests/LoginTests.cs");
        Assert.NotNull(mismatch);
        Assert.Contains("points elsewhere", mismatch.Issue);
    }

    [Fact]
    public void Reconcile_ValidBidirectionalLink_NoIssues()
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
                    new TestIndexEntry
                    {
                        Id = "TC-001",
                        File = "TC-001.md",
                        Title = "Valid Link Test",
                        Priority = "P1",
                        SourceRefs = ["tests/LoginTests.cs"]
                    }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/LoginTests.cs"] = new(
                "tests/LoginTests.cs",
                ["TC-001"],
                [new TestReference("TC-001", 10)])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.ValidLinks);
        Assert.Equal("TC-001", result.ValidLinks[0].Source);
        Assert.Equal("tests/LoginTests.cs", result.ValidLinks[0].Target);
        Assert.Equal(LinkStatus.Valid, result.ValidLinks[0].Status);
        Assert.Empty(result.UnlinkedTests);
        Assert.Empty(result.OrphanedAutomation);
        Assert.Empty(result.BrokenLinks);
        Assert.Empty(result.Mismatches);
    }

    [Fact]
    public void Reconcile_MultipleSuites_ProcessesAll()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["auth"] = new()
            {
                Suite = "auth",
                GeneratedAt = DateTime.UtcNow,
                Tests = [new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Auth Test", Priority = "P1", SourceRefs = [] }]
            },
            ["billing"] = new()
            {
                Suite = "billing",
                GeneratedAt = DateTime.UtcNow,
                Tests = [new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Billing Test", Priority = "P1", SourceRefs = [] }]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>();

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Equal(2, result.UnlinkedTests.Count);
        Assert.Contains(result.UnlinkedTests, t => t.TestId == "TC-001" && t.Suite == "auth");
        Assert.Contains(result.UnlinkedTests, t => t.TestId == "TC-002" && t.Suite == "billing");
    }

    [Fact]
    public void Reconcile_AutomatedCount_CalculatesCorrectly()
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
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Automated", Priority = "P1", SourceRefs = ["tests/T1.cs"] },
                    new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Manual", Priority = "P1", SourceRefs = [] },
                    new TestIndexEntry { Id = "TC-003", File = "TC-003.md", Title = "Automated", Priority = "P1", SourceRefs = ["tests/T2.cs"] }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/T1.cs"] = new("tests/T1.cs", ["TC-001"], [new TestReference("TC-001", 1)]),
            ["tests/T2.cs"] = new("tests/T2.cs", ["TC-003"], [new TestReference("TC-003", 1)])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Equal(2, result.AutomatedCount);
    }

    [Fact]
    public void Reconcile_CaseInsensitiveTestIdMatching()
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
                    new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test", Priority = "P1", SourceRefs = ["tests/Test.cs"] }
                ]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/Test.cs"] = new(
                "tests/Test.cs",
                ["tc-001"], // Lowercase
                [new TestReference("tc-001", 10)])
        };

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Single(result.ValidLinks);
        Assert.Equal(LinkStatus.Valid, result.ValidLinks[0].Status);
    }

    [Fact]
    public void Reconcile_OrdersOutputByIdAndSuite()
    {
        // Arrange
        var suiteIndexes = new Dictionary<string, MetadataIndex>
        {
            ["z-suite"] = new()
            {
                Suite = "z-suite",
                GeneratedAt = DateTime.UtcNow,
                Tests = [new TestIndexEntry { Id = "TC-002", File = "TC-002.md", Title = "Test 2", Priority = "P1", SourceRefs = [] }]
            },
            ["a-suite"] = new()
            {
                Suite = "a-suite",
                GeneratedAt = DateTime.UtcNow,
                Tests = [new TestIndexEntry { Id = "TC-001", File = "TC-001.md", Title = "Test 1", Priority = "P1", SourceRefs = [] }]
            }
        };

        var automationFiles = new Dictionary<string, AutomationFileInfo>();

        // Act
        var result = _reconciler.Reconcile(suiteIndexes, automationFiles);

        // Assert
        Assert.Equal("a-suite", result.UnlinkedTests[0].Suite);
        Assert.Equal("z-suite", result.UnlinkedTests[1].Suite);
    }
}
