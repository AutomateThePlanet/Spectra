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
        // Spec 059: the seam supports two flows (main/focus and from-description); the
        // in-process --from-suggestions sub-mode was retired with the in-process generator.
        Assert.Contains("How to choose between generation flows", SkillContentText);
        Assert.Contains("--from-description", SkillContentText);
        Assert.Contains("--focus", SkillContentText);
    }

    [Fact]
    public void GenerateSkill_FromDescriptionUsesSeamCommand()
    {
        // Spec 059: from-description routes through the compile-prompt seam, not the
        // retired in-process `spectra ai generate --from-description`.
        var content = SkillContentText;
        Assert.Contains("compile-prompt --suite", content);
        Assert.Contains("--from-description", content);
        Assert.Contains("--output-format json", content);
        // No in-process generation invocations remain (the seam replaced them).
        Assert.DoesNotContain("ai generate --from-description", content);
        Assert.DoesNotContain("ai generate --count", content);
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
    public void GenerationAgent_DocumentsExploreAndFromDescriptionIntents()
    {
        // Spec 059: two routed intents survive on the seam — explore-area (--focus) and
        // create-specific (--from-description). The --from-suggestions intent was retired
        // with the in-process generator.
        var content = AgentContentText;
        Assert.Contains("Intent 1", content);
        Assert.Contains("Intent 2", content);
        Assert.DoesNotContain("--from-suggestions", content);
    }

    [Fact]
    public void GenerationAgent_RoutingForbidsCountQuestions()
    {
        var content = AgentContentText;
        // Spec 056: the Copilot "Do NOT ask clarifying questions" line is translated to Claude
        // Code's confirmation model — the intent (don't stop to confirm count/scope) is preserved.
        Assert.Contains("count or scope", content);
        Assert.Contains("without a needless confirmation", content);
    }
}
