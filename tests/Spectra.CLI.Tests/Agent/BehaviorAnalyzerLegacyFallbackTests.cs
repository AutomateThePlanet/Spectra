using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 037: when no PromptTemplateLoader is wired up, the legacy hardcoded
/// fallback prompt must still teach the AI about ISTQB techniques and request
/// the `technique` field in the JSON output.
/// </summary>
public class BehaviorAnalyzerLegacyFallbackTests
{
    private static string FallbackPrompt()
    {
        var docs = new List<SourceDocument>
        {
            new() { Path = "docs/sample.md", Title = "Sample", Content = "Sample doc content.", Sections = [] }
        };
        return BehaviorAnalyzer.BuildAnalysisPrompt(docs, focusArea: null, config: null, templateLoader: null);
    }

    [Fact]
    public void LegacyFallback_ContainsAllSixTechniqueShortCodes()
    {
        var prompt = FallbackPrompt();

        Assert.Contains("Equivalence Partitioning", prompt);
        Assert.Contains("Boundary Value Analysis", prompt);
        Assert.Contains("Decision Table", prompt);
        Assert.Contains("State Transition", prompt);
        Assert.Contains("Error Guessing", prompt);
        Assert.Contains("Use Case", prompt);
    }

    [Fact]
    public void LegacyFallback_RequestsTechniqueFieldInJsonOutput()
    {
        var prompt = FallbackPrompt();

        Assert.Contains("technique", prompt);
        Assert.Contains("\"technique\":", prompt);
    }

    [Fact]
    public void LegacyFallback_Contains40PercentDistributionGuideline()
    {
        var prompt = FallbackPrompt();

        Assert.Contains("40%", prompt);
    }
}
