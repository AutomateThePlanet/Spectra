using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.Core.Tests.Coverage;

public class RequirementsCoverageAnalyzerTests
{
    private readonly RequirementsCoverageAnalyzer _analyzer = new();

    [Fact]
    public async Task Analyze_NoRequirementsFile_ReportsFromTests()
    {
        var tests = new List<TestCase>
        {
            CreateTestCase("TC-001", ["REQ-001", "REQ-002"]),
            CreateTestCase("TC-002", ["REQ-001"])
        };

        var result = await _analyzer.AnalyzeAsync("/nonexistent.yaml", tests);

        Assert.False(result.HasRequirementsFile);
        Assert.Equal(2, result.TotalRequirements); // REQ-001, REQ-002
        Assert.Equal(2, result.CoveredRequirements); // All discovered from tests are "covered"
    }

    [Fact]
    public async Task Analyze_WithRequirementsFile_CrossReferences()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                requirements:
                  - id: REQ-001
                    title: "Login must work"
                  - id: REQ-002
                    title: "Logout must work"
                  - id: REQ-003
                    title: "Admin panel"
                """);

            var tests = new List<TestCase>
            {
                CreateTestCase("TC-001", ["REQ-001"]),
                CreateTestCase("TC-002", ["REQ-002"])
            };

            var result = await _analyzer.AnalyzeAsync(tempFile, tests);

            Assert.True(result.HasRequirementsFile);
            Assert.Equal(3, result.TotalRequirements);
            Assert.Equal(2, result.CoveredRequirements);
            Assert.Equal(66.67m, result.Percentage);

            var uncovered = result.Details.First(d => d.Id == "REQ-003");
            Assert.False(uncovered.Covered);
            Assert.Empty(uncovered.Tests);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Analyze_NoRequirementsAnywhere_ReturnsEmpty()
    {
        var result = await _analyzer.AnalyzeAsync("/nonexistent.yaml", []);

        Assert.Equal(0, result.TotalRequirements);
        Assert.Equal(0m, result.Percentage);
        Assert.False(result.HasRequirementsFile);
    }

    [Fact]
    public async Task Analyze_AllRequirementsCovered_Returns100()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                requirements:
                  - id: REQ-001
                    title: "Login"
                """);

            var tests = new List<TestCase>
            {
                CreateTestCase("TC-001", ["REQ-001"])
            };

            var result = await _analyzer.AnalyzeAsync(tempFile, tests);

            Assert.Equal(100m, result.Percentage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static TestCase CreateTestCase(string id, IReadOnlyList<string> requirements) => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Priority = Priority.Medium,
        Title = $"Test {id}",
        ExpectedResult = "Expected",
        Requirements = requirements
    };
}
