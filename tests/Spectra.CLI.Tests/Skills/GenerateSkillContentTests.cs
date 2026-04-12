using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 033: verifies the spectra-generate SKILL and spectra-generation agent prompt
/// contain the from-description flow and intent routing rules.
/// </summary>
public class GenerateSkillContentTests
{
    private static string SkillContentText => SkillContent.Generate;
    private static string AgentContentText => AgentContent.GenerationAgent;

    // -- spectra-generate SKILL ---------------------------------------------

    [Fact]
    public void GenerateSkill_HasFromDescriptionSection()
    {
        Assert.Contains("create a specific test case", SkillContentText);
    }

    [Fact]
    public void GenerateSkill_HasIntentRoutingTable()
    {
        Assert.Contains("How to choose between generation flows", SkillContentText);
        Assert.Contains("--from-description", SkillContentText);
        Assert.Contains("--focus", SkillContentText);
        Assert.Contains("--from-suggestions", SkillContentText);
    }

    [Fact]
    public void GenerateSkill_FromDescriptionUsesCorrectFlags()
    {
        // The documented from-description command must include all SKILL-standard flags.
        var content = SkillContentText;
        Assert.Contains("--from-description", content);
        Assert.Contains("--no-interaction", content);
        Assert.Contains("--output-format json", content);
        Assert.Contains("--verbosity quiet", content);
    }

    [Fact]
    public void GenerateSkill_FromDescriptionStatesNoAnalysisNoCount()
    {
        var content = SkillContentText;
        Assert.Contains("Do NOT run analysis", content);
        Assert.Contains("Do NOT ask", content);
        Assert.Contains("1 test", content);
    }

    [Fact]
    public void GenerateSkill_RoutingKeyRuleExplainsTopicVsScenario()
    {
        Assert.Contains("Key rule", SkillContentText);
    }

    // -- spectra-generation agent prompt ------------------------------------

    [Fact]
    public void GenerationAgent_HasIntentRoutingSection()
    {
        Assert.Contains("Test Creation Intent Routing", AgentContentText);
    }

    [Fact]
    public void GenerationAgent_RoutesToFromDescriptionForSpecificTest()
    {
        var content = AgentContentText;
        Assert.Contains("--from-description", content);
        Assert.Contains("Add a test for", content);
    }

    [Fact]
    public void GenerationAgent_RoutesToFocusForExploreArea()
    {
        var content = AgentContentText;
        Assert.Contains("--focus", content);
        Assert.Contains("Generate test cases for", content);
    }

    [Fact]
    public void GenerationAgent_DocumentsAllThreeIntents()
    {
        var content = AgentContentText;
        Assert.Contains("Intent 1", content);
        Assert.Contains("Intent 2", content);
        Assert.Contains("Intent 3", content);
    }

    [Fact]
    public void GenerationAgent_RoutingForbidsCountQuestions()
    {
        var content = AgentContentText;
        Assert.Contains("Do NOT ask", content);
        // Either "count" or "scope" should appear in the forbidden-questions context
        Assert.True(content.Contains("count", StringComparison.OrdinalIgnoreCase)
                 || content.Contains("scope", StringComparison.OrdinalIgnoreCase));
    }
}
