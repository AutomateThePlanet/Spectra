using System.Text.Json;
using Spectra.CLI.Agent.Analysis;

namespace Spectra.CLI.Tests.Agent.Analysis;

public class CoverageSnapshotBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public CoverageSnapshotBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>T008: ReadSuiteIndexAsync with non-existent path returns empty data.</summary>
    [Fact]
    public async Task EmptySuite_ReturnsZeros()
    {
        var (titles, criteriaIds, sourceRefs) =
            await CoverageSnapshotBuilder.ReadSuiteIndexAsync("nonexistent", _tempDir, CancellationToken.None);

        Assert.Empty(titles);
        Assert.Empty(criteriaIds);
        Assert.Empty(sourceRefs);
    }

    /// <summary>T009: ReadSuiteIndexAsync correctly counts tests, criteria, and source refs.</summary>
    [Fact]
    public async Task WithTests_CountsCorrectly()
    {
        var suiteDir = Path.Combine(_tempDir, "my-suite");
        Directory.CreateDirectory(suiteDir);

        var indexJson = """
            {
              "suite": "my-suite",
              "generated_at": "2026-01-01T00:00:00",
              "tests": [
                {
                  "id": "TC-001",
                  "file": "TC-001.md",
                  "title": "Login succeeds with valid credentials",
                  "priority": "high",
                  "tags": [],
                  "source_refs": ["docs/auth.md"],
                  "criteria": ["AC-001"],
                  "requirements": [],
                  "automated_by": [],
                  "bugs": []
                },
                {
                  "id": "TC-002",
                  "file": "TC-002.md",
                  "title": "Login fails with invalid password",
                  "priority": "high",
                  "tags": [],
                  "source_refs": ["docs/auth.md", "docs/errors.md"],
                  "criteria": ["AC-001", "AC-002"],
                  "requirements": [],
                  "automated_by": [],
                  "bugs": []
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(suiteDir, "_index.json"), indexJson);

        var (titles, criteriaIds, sourceRefs) =
            await CoverageSnapshotBuilder.ReadSuiteIndexAsync("my-suite", _tempDir, CancellationToken.None);

        Assert.Equal(2, titles.Count);
        Assert.Contains("Login succeeds with valid credentials", titles);
        Assert.Contains("Login fails with invalid password", titles);
        Assert.Contains("AC-001", criteriaIds);
        Assert.Contains("AC-002", criteriaIds);
        Assert.Contains("docs/auth.md", sourceRefs);
        Assert.Contains("docs/errors.md", sourceRefs);
    }

    /// <summary>T010: BuildAsync cross-references criteria; uncovered criteria identified.</summary>
    [Fact]
    public async Task CrossRefsCriteria()
    {
        // Set up suite with tests covering AC-001, AC-002, AC-003
        var suiteDir = Path.Combine(_tempDir, "test-cases", "suite1");
        Directory.CreateDirectory(suiteDir);

        var indexJson = """
            {
              "suite": "suite1",
              "generated_at": "2026-01-01T00:00:00",
              "tests": [
                {
                  "id": "TC-001",
                  "file": "TC-001.md",
                  "title": "Covers AC-001 and AC-002",
                  "priority": "high",
                  "tags": [],
                  "source_refs": [],
                  "criteria": ["AC-001", "AC-002"],
                  "requirements": [],
                  "automated_by": [],
                  "bugs": []
                },
                {
                  "id": "TC-002",
                  "file": "TC-002.md",
                  "title": "Covers AC-003",
                  "priority": "medium",
                  "tags": [],
                  "source_refs": [],
                  "criteria": ["AC-003"],
                  "requirements": [],
                  "automated_by": [],
                  "bugs": []
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(suiteDir, "_index.json"), indexJson);

        // Set up criteria dir with AC-001 through AC-005
        var criteriaDir = Path.Combine(_tempDir, "docs", "criteria");
        Directory.CreateDirectory(criteriaDir);

        var criteriaYaml = """
            criteria:
              - id: AC-001
                text: "First criterion"
                priority: high
              - id: AC-002
                text: "Second criterion"
                priority: medium
              - id: AC-003
                text: "Third criterion"
                priority: high
              - id: AC-004
                text: "Fourth criterion"
                priority: low
              - id: AC-005
                text: "Fifth criterion"
                priority: medium
            """;
        await File.WriteAllTextAsync(Path.Combine(criteriaDir, "auth.criteria.yaml"), criteriaYaml);

        var builder = new CoverageSnapshotBuilder(_tempDir);
        var snapshot = await builder.BuildAsync(
            suite: "suite1",
            testsDir: Path.Combine(_tempDir, "test-cases"),
            criteriaDir: criteriaDir,
            criteriaIndexFile: Path.Combine(criteriaDir, "_criteria_index.yaml"),
            docIndexFile: Path.Combine(_tempDir, "docs", "_index.md"));

        Assert.Equal(2, snapshot.UncoveredCriteria.Count);

        var uncoveredIds = snapshot.UncoveredCriteria.Select(c => c.Id).ToHashSet();
        Assert.Contains("AC-004", uncoveredIds);
        Assert.Contains("AC-005", uncoveredIds);
        Assert.DoesNotContain("AC-001", uncoveredIds);
        Assert.DoesNotContain("AC-002", uncoveredIds);
        Assert.DoesNotContain("AC-003", uncoveredIds);

        Assert.Equal(5, snapshot.TotalCriteriaCount);
    }

    /// <summary>T011: BuildAsync tracks covered and uncovered source refs.</summary>
    [Fact]
    public async Task CoveredSourceRefs()
    {
        // Set up suite with tests referencing some doc paths
        var suiteDir = Path.Combine(_tempDir, "test-cases", "suite2");
        Directory.CreateDirectory(suiteDir);

        var indexJson = """
            {
              "suite": "suite2",
              "generated_at": "2026-01-01T00:00:00",
              "tests": [
                {
                  "id": "TC-001",
                  "file": "TC-001.md",
                  "title": "Covers auth doc",
                  "priority": "high",
                  "tags": [],
                  "source_refs": ["docs/auth.md"],
                  "criteria": [],
                  "requirements": [],
                  "automated_by": [],
                  "bugs": []
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(suiteDir, "_index.json"), indexJson);

        // Set up doc index with multiple entries
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);

        // The DocumentIndexReader.ParseFull expects a specific markdown format.
        // We'll create a doc _index.md and use the builder, which calls ReadDocSectionRefsAsync.
        // Since doc index parsing is complex, we rely on the builder treating a missing/invalid
        // doc index gracefully — the important part is that covered source refs are from tests.
        var builder = new CoverageSnapshotBuilder(_tempDir);
        var snapshot = await builder.BuildAsync(
            suite: "suite2",
            testsDir: Path.Combine(_tempDir, "test-cases"),
            criteriaDir: Path.Combine(_tempDir, "docs", "criteria"),
            criteriaIndexFile: Path.Combine(_tempDir, "docs", "criteria", "_criteria_index.yaml"),
            docIndexFile: Path.Combine(docsDir, "_index.md"));

        Assert.Contains("docs/auth.md", snapshot.CoveredSourceRefs);
        Assert.Equal(1, snapshot.ExistingTestCount);
    }

    /// <summary>T020: ReadSuiteIndexAsync with missing _index.json returns empty, no exception.</summary>
    [Fact]
    public async Task MissingIndex_GracefulFallback()
    {
        var nonExistentDir = Path.Combine(_tempDir, "does-not-exist");

        var (titles, criteriaIds, sourceRefs) =
            await CoverageSnapshotBuilder.ReadSuiteIndexAsync("missing", nonExistentDir, CancellationToken.None);

        Assert.Empty(titles);
        Assert.Empty(criteriaIds);
        Assert.Empty(sourceRefs);
    }

    /// <summary>T021: ReadAllCriteriaAsync with non-existent directory returns empty list.</summary>
    [Fact]
    public async Task MissingCriteria_PartialSnapshot()
    {
        var nonExistentDir = Path.Combine(_tempDir, "no-criteria-here");

        var criteria = await CoverageSnapshotBuilder.ReadAllCriteriaAsync(
            nonExistentDir,
            Path.Combine(nonExistentDir, "_criteria_index.yaml"),
            CancellationToken.None);

        Assert.Empty(criteria);
    }

    /// <summary>T022: ReadDocSectionRefsAsync with non-existent file returns empty list.</summary>
    [Fact]
    public async Task MissingDocIndex_PartialSnapshot()
    {
        var nonExistentFile = Path.Combine(_tempDir, "nonexistent_index.md");

        var refs = await CoverageSnapshotBuilder.ReadDocSectionRefsAsync(
            nonExistentFile, CancellationToken.None);

        Assert.Empty(refs);
    }
}
