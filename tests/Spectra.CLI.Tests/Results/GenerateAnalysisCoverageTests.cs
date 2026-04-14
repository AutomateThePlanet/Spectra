using System.Text.Json;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Results;

public sealed class GenerateAnalysisCoverageTests
{
    [Fact]
    public void NewFields_Serialize_RoundTrip()
    {
        var analysis = new GenerateAnalysis
        {
            TotalBehaviors = 8,
            AlreadyCovered = 231,
            Recommended = 8,
            ExistingTestCount = 231,
            TotalCriteria = 41,
            CoveredCriteria = 38,
            UncoveredCriteria = 3,
            UncoveredCriteriaIds = ["AC-039", "AC-040", "AC-041"]
        };

        var json = JsonSerializer.Serialize(analysis);
        var deserialized = JsonSerializer.Deserialize<GenerateAnalysis>(json)!;

        Assert.Equal(231, deserialized.ExistingTestCount);
        Assert.Equal(41, deserialized.TotalCriteria);
        Assert.Equal(38, deserialized.CoveredCriteria);
        Assert.Equal(3, deserialized.UncoveredCriteria);
        Assert.Equal(["AC-039", "AC-040", "AC-041"], deserialized.UncoveredCriteriaIds);
    }

    [Fact]
    public void BackwardCompat_MissingFields_DefaultToZero()
    {
        // Old JSON without new fields
        var json = """{"total_behaviors":142,"already_covered":3,"recommended":139}""";
        var deserialized = JsonSerializer.Deserialize<GenerateAnalysis>(json)!;

        Assert.Equal(0, deserialized.ExistingTestCount);
        Assert.Equal(0, deserialized.TotalCriteria);
        Assert.Equal(0, deserialized.CoveredCriteria);
        Assert.Equal(0, deserialized.UncoveredCriteria);
        Assert.Null(deserialized.UncoveredCriteriaIds);
    }

    [Fact]
    public void NewFields_OmittedWhenDefault()
    {
        var analysis = new GenerateAnalysis
        {
            TotalBehaviors = 10,
            AlreadyCovered = 5,
            Recommended = 5
        };

        var json = JsonSerializer.Serialize(analysis);
        Assert.DoesNotContain("existing_test_count", json);
        Assert.DoesNotContain("uncovered_criteria_ids", json);
    }
}
