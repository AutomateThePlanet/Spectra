using System.Text.Json;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Results;

/// <summary>
/// Spec 037: GenerateAnalysis must serialize a `technique_breakdown` JSON
/// object alongside the existing `breakdown` map. Empty case serializes as {}.
/// </summary>
public class GenerateResultTechniqueBreakdownTests
{
    [Fact]
    public void GenerateAnalysis_WithTechniques_SerializesTechniqueBreakdown()
    {
        var analysis = new GenerateAnalysis
        {
            TotalBehaviors = 14,
            AlreadyCovered = 0,
            Recommended = 14,
            Breakdown = new Dictionary<string, int> { ["boundary"] = 8, ["happy_path"] = 6 },
            TechniqueBreakdown = new Dictionary<string, int> { ["BVA"] = 8, ["UC"] = 6 }
        };

        var json = JsonSerializer.Serialize(analysis);

        Assert.Contains("\"technique_breakdown\"", json);
        Assert.Contains("\"BVA\":8", json);
        Assert.Contains("\"UC\":6", json);
    }

    [Fact]
    public void GenerateAnalysis_EmptyTechniqueBreakdown_SerializesAsEmptyObject()
    {
        var analysis = new GenerateAnalysis
        {
            TotalBehaviors = 0,
            AlreadyCovered = 0,
            Recommended = 0
        };

        var json = JsonSerializer.Serialize(analysis);

        // Stable contract for SKILL/CI consumers — field is always present
        Assert.Contains("\"technique_breakdown\":{}", json);
    }

    [Fact]
    public void GenerateAnalysis_TechniqueBreakdownDefaultsToEmpty()
    {
        var analysis = new GenerateAnalysis();
        Assert.NotNull(analysis.TechniqueBreakdown);
        Assert.Empty(analysis.TechniqueBreakdown);
    }
}
