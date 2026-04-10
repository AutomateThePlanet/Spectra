using Spectra.CLI.Progress;

namespace Spectra.CLI.Tests.Progress;

/// <summary>
/// Spec 037: progress page must render a Technique Breakdown section beneath
/// the Category Breakdown when the analysis result includes one. The section
/// is suppressed when the technique_breakdown map is empty.
/// </summary>
public class ProgressPageTechniqueBreakdownTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string CreateTempHtmlPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}.html");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".tmp"); } catch { }
        }
    }

    [Fact]
    public void ProgressPage_WithTechniqueBreakdown_RendersTechniqueSection()
    {
        var path = CreateTempHtmlPath();
        var json = """
            {
              "status": "analyzed",
              "suite": "checkout",
              "analysis": {
                "total_behaviors": 14,
                "already_covered": 0,
                "recommended": 14,
                "breakdown": {"boundary": 8, "happy_path": 6},
                "technique_breakdown": {"BVA": 8, "UC": 6}
              }
            }
            """;

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var html = File.ReadAllText(path);
        Assert.Contains("Technique Breakdown", html);
        Assert.Contains("Boundary Value Analysis", html);
        Assert.Contains("Use Case", html);
    }

    [Fact]
    public void ProgressPage_EmptyTechniqueBreakdown_OmitsTechniqueSection()
    {
        var path = CreateTempHtmlPath();
        var json = """
            {
              "status": "analyzed",
              "suite": "checkout",
              "analysis": {
                "total_behaviors": 5,
                "already_covered": 0,
                "recommended": 5,
                "breakdown": {"happy_path": 5},
                "technique_breakdown": {}
              }
            }
            """;

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var html = File.ReadAllText(path);
        Assert.DoesNotContain("Technique Breakdown", html);
    }

    [Fact]
    public void ProgressPage_TechniqueBreakdown_RendersInFixedDisplayOrder()
    {
        var path = CreateTempHtmlPath();
        var json = """
            {
              "status": "analyzed",
              "suite": "checkout",
              "analysis": {
                "total_behaviors": 30,
                "already_covered": 0,
                "recommended": 30,
                "breakdown": {"boundary": 30},
                "technique_breakdown": {"UC": 5, "BVA": 10, "ST": 3, "EP": 7, "DT": 2, "EG": 3}
              }
            }
            """;

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var html = File.ReadAllText(path);
        // Fixed order: BVA, EP, DT, ST, EG, UC
        var bva = html.IndexOf("Boundary Value Analysis", StringComparison.Ordinal);
        var ep = html.IndexOf("Equivalence Partitioning", StringComparison.Ordinal);
        var dt = html.IndexOf("Decision Table", StringComparison.Ordinal);
        var st = html.IndexOf("State Transition", StringComparison.Ordinal);
        var eg = html.IndexOf("Error Guessing", StringComparison.Ordinal);
        var uc = html.IndexOf("Use Case", StringComparison.Ordinal);

        Assert.True(bva < ep, "BVA should appear before EP");
        Assert.True(ep < dt, "EP should appear before DT");
        Assert.True(dt < st, "DT should appear before ST");
        Assert.True(st < eg, "ST should appear before EG");
        Assert.True(eg < uc, "EG should appear before UC");
    }
}
