using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.Core.Tests.Coverage;

public class AcceptanceCriteriaCoverageAnalyzerTests : IDisposable
{
    private readonly AcceptanceCriteriaCoverageAnalyzer _analyzer = new();
    private readonly string _tempDir;

    public AcceptanceCriteriaCoverageAnalyzerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-criteria-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task AnalyzeFromDirectory_ReadsPerDocumentFiles()
    {
        // Arrange
        await WriteCriteriaIndexAsync(new[] { "checkout.criteria.yaml", "payments.criteria.yaml" });
        await WriteCriteriaFileAsync("checkout.criteria.yaml", new[]
        {
            ("AC-CHECKOUT-001", "User can add items to cart"),
            ("AC-CHECKOUT-002", "User can remove items from cart"),
            ("AC-CHECKOUT-003", "Cart total is calculated correctly")
        });
        await WriteCriteriaFileAsync("payments.criteria.yaml", new[]
        {
            ("AC-PAYMENTS-001", "User can pay with credit card"),
            ("AC-PAYMENTS-002", "Payment receipt is sent")
        });

        var indexPath = Path.Combine(_tempDir, "_criteria_index.yaml");

        // Act
        var result = await _analyzer.AnalyzeFromDirectoryAsync(_tempDir, indexPath, [], default);

        // Assert
        Assert.Equal(5, result.TotalCriteria);
        Assert.True(result.HasCriteriaFile);
    }

    [Fact]
    public async Task AnalyzeFromDirectory_MatchesTestFrontmatter()
    {
        // Arrange
        await WriteCriteriaIndexAsync(new[] { "checkout.criteria.yaml" });
        await WriteCriteriaFileAsync("checkout.criteria.yaml", new[]
        {
            ("AC-CHECKOUT-001", "Add to cart"),
            ("AC-CHECKOUT-002", "Remove from cart"),
            ("AC-CHECKOUT-003", "Calculate total")
        });

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", criteria: ["AC-CHECKOUT-001"]),
            CreateTestCase("TC-002", criteria: ["AC-CHECKOUT-002"])
        };

        var indexPath = Path.Combine(_tempDir, "_criteria_index.yaml");

        // Act
        var result = await _analyzer.AnalyzeFromDirectoryAsync(_tempDir, indexPath, tests, default);

        // Assert
        Assert.Equal(3, result.TotalCriteria);
        Assert.Equal(2, result.CoveredCriteria);
    }

    [Fact]
    public async Task AnalyzeFromDirectory_EmptyDir_ReturnsZero()
    {
        // Arrange — index with no sources
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "_criteria_index.yaml"),
            "version: 1\ntotal_criteria: 0\nsources: []");

        var indexPath = Path.Combine(_tempDir, "_criteria_index.yaml");

        // Act
        var result = await _analyzer.AnalyzeFromDirectoryAsync(_tempDir, indexPath, [], default);

        // Assert
        Assert.Equal(0, result.TotalCriteria);
        Assert.Equal(0, result.CoveredCriteria);
    }

    [Fact]
    public async Task AnalyzeFromDirectory_NoTestsLinked_AllUncovered()
    {
        // Arrange
        await WriteCriteriaIndexAsync(new[] { "checkout.criteria.yaml" });
        await WriteCriteriaFileAsync("checkout.criteria.yaml", new[]
        {
            ("AC-CHECKOUT-001", "Add to cart"),
            ("AC-CHECKOUT-002", "Remove from cart")
        });

        var indexPath = Path.Combine(_tempDir, "_criteria_index.yaml");

        // Act
        var result = await _analyzer.AnalyzeFromDirectoryAsync(_tempDir, indexPath, [], default);

        // Assert
        Assert.Equal(2, result.TotalCriteria);
        Assert.Equal(0, result.CoveredCriteria);
        Assert.All(result.Details, d => Assert.False(d.Covered));
    }

    [Fact]
    public async Task AnalyzeFromDirectory_MultipleTestsSameCriterion_CountsOnce()
    {
        // Arrange
        await WriteCriteriaIndexAsync(new[] { "checkout.criteria.yaml" });
        await WriteCriteriaFileAsync("checkout.criteria.yaml", new[]
        {
            ("AC-CHECKOUT-001", "Add to cart")
        });

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", criteria: ["AC-CHECKOUT-001"]),
            CreateTestCase("TC-002", criteria: ["AC-CHECKOUT-001"])
        };

        var indexPath = Path.Combine(_tempDir, "_criteria_index.yaml");

        // Act
        var result = await _analyzer.AnalyzeFromDirectoryAsync(_tempDir, indexPath, tests, default);

        // Assert
        Assert.Equal(1, result.TotalCriteria);
        Assert.Equal(1, result.CoveredCriteria);
        var detail = result.Details.Single();
        Assert.Equal(2, detail.Tests.Count); // Both tests linked
    }

    [Fact]
    public async Task Analyze_UsesLegacyRequirementsField()
    {
        // Arrange — tests use the legacy Requirements field instead of Criteria
        await WriteCriteriaIndexAsync(new[] { "checkout.criteria.yaml" });
        await WriteCriteriaFileAsync("checkout.criteria.yaml", new[]
        {
            ("AC-CHECKOUT-001", "Add to cart")
        });

        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", requirements: ["AC-CHECKOUT-001"])
        };

        var indexPath = Path.Combine(_tempDir, "_criteria_index.yaml");

        // Act
        var result = await _analyzer.AnalyzeFromDirectoryAsync(_tempDir, indexPath, tests, default);

        // Assert — should still be covered via legacy Requirements field
        Assert.Equal(1, result.CoveredCriteria);
    }

    private async Task WriteCriteriaIndexAsync(string[] sourceFiles)
    {
        var sources = string.Join("\n", sourceFiles.Select(f =>
            $"  - source_doc: \"{f}\"\n    file: \"{f}\"\n    doc_hash: \"abc123\"\n    criteria_count: 0"));
        var yaml = $"version: 1\ntotal_criteria: 0\nsources:\n{sources}";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "_criteria_index.yaml"), yaml);
    }

    private async Task WriteCriteriaFileAsync(string fileName, (string id, string text)[] criteria)
    {
        var items = string.Join("\n", criteria.Select(c =>
            $"  - id: {c.id}\n    text: \"{c.text}\""));
        var yaml = $"criteria:\n{items}";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, fileName), yaml);
    }

    private static TestCase CreateTestCase(
        string id,
        IReadOnlyList<string>? requirements = null,
        IReadOnlyList<string>? criteria = null) => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Priority = Priority.Medium,
        Title = $"Test {id}",
        ExpectedResult = "Expected",
        Requirements = requirements ?? [],
        Criteria = criteria ?? []
    };
}
