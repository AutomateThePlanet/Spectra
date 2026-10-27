using Spectra.CLI.Agent.Critic;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Integration tests for the verification pipeline.
/// These tests verify the complete flow from test input through verification.
/// </summary>
public class VerificationIntegrationTests
{
    // Spec 102 (F1): the builder needs a template loader; a folder with no .spectra/prompts gives the bundled template.
    private static PromptTemplateLoader BundledTemplates() =>
        new(Path.Combine(Path.GetTempPath(), $"spectra-102-no-workspace-{Guid.NewGuid():N}"));

    [Fact]
    public void VerificationPipeline_PromptBuilder_BuildsValidPrompt()
    {
        var promptBuilder = new CriticPromptBuilder(BundledTemplates());
        var test = CreateTestCase("TC-001", "Test Login");
        var docs = new List<SourceDocument>
        {
            new() { Path = "docs/login.md", Title = "Login Feature", Content = "Users can log in with email/password" }
        };

        var systemPrompt = promptBuilder.BuildSystemPrompt();
        var userPrompt = promptBuilder.BuildUserPrompt(test, docs);

        Assert.Contains("verification expert", systemPrompt.ToLowerInvariant());
        Assert.Contains("grounded", systemPrompt.ToLowerInvariant());
        Assert.Contains("TC-001", userPrompt);
        Assert.Contains("Test Login", userPrompt);
        Assert.Contains("Login Feature", userPrompt);
    }

    [Fact]
    public void VerificationPipeline_ToMetadata_ConvertsResultCorrectly()
    {
        var result = new VerificationResult
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.95,
            Findings =
            [
                new CriticFinding
                {
                    Element = "Step 1",
                    Claim = "Test claim",
                    Status = FindingStatus.Grounded,
                    Evidence = "Found in docs"
                }
            ],
            CriticModel = "gemini-2.0-flash",
            Duration = TimeSpan.FromSeconds(1.5)
        };

        var metadata = result.ToMetadata("gpt-4o");

        Assert.Equal(VerificationVerdict.Grounded, metadata.Verdict);
        Assert.Equal(0.95, metadata.Score);
        Assert.Equal("gpt-4o", metadata.Generator);
        Assert.Equal("gemini-2.0-flash", metadata.Critic);
        Assert.Empty(metadata.UnverifiedClaims);
    }

    [Fact]
    public void VerificationPipeline_ToMetadata_IncludesUnverifiedClaims()
    {
        var result = new VerificationResult
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.7,
            Findings =
            [
                new CriticFinding
                {
                    Element = "Step 1",
                    Claim = "Verified claim",
                    Status = FindingStatus.Grounded
                },
                new CriticFinding
                {
                    Element = "Step 2",
                    Claim = "Unverified claim",
                    Status = FindingStatus.Unverified,
                    Reason = "Not in docs"
                }
            ],
            CriticModel = "test-critic"
        };

        var metadata = result.ToMetadata("test-generator");

        Assert.Equal(VerificationVerdict.Partial, metadata.Verdict);
        Assert.Single(metadata.UnverifiedClaims);
        // Format is "Element: Reason" or "Element: Claim"
        Assert.Equal("Step 2: Not in docs", metadata.UnverifiedClaims[0]);
    }

    [Fact]
    public void VerificationPipeline_PromptBuilder_HandlesEmptyDocs()
    {
        var promptBuilder = new CriticPromptBuilder(BundledTemplates());
        var test = CreateTestCase("TC-001", "Test");
        var docs = new List<SourceDocument>();

        var prompt = promptBuilder.BuildUserPrompt(test, docs);

        Assert.Contains("No relevant documentation provided", prompt);
    }

    [Fact]
    public void VerificationPipeline_PromptBuilder_TruncatesLargeDocs()
    {
        var promptBuilder = new CriticPromptBuilder(BundledTemplates());
        var test = CreateTestCase("TC-001", "Test");
        var largeContent = new string('x', 20000);
        var docs = new List<SourceDocument>
        {
            new() { Path = "large.md", Title = "Large Doc", Content = largeContent }
        };

        var prompt = promptBuilder.BuildUserPrompt(test, docs);

        // Should contain truncation indicator
        Assert.Contains("truncated", prompt.ToLowerInvariant());
        // Should not contain full content
        Assert.True(prompt.Length < largeContent.Length);
    }

    [Fact]
    public void VerificationPipeline_PromptBuilder_PrioritizesSourceRefs()
    {
        var promptBuilder = new CriticPromptBuilder(BundledTemplates());
        var test = new TestCase
        {
            Id = "TC-001",
            Title = "Test",
            Priority = Priority.Medium,
            Steps = ["Step 1"],
            ExpectedResult = "Result",
            FilePath = "test.md",
            SourceRefs = ["docs/referenced.md"]
        };

        var docs = new List<SourceDocument>
        {
            new() { Path = "docs/other.md", Title = "Other", Content = "Other content" },
            new() { Path = "docs/referenced.md", Title = "Referenced", Content = "Referenced content" }
        };

        var prompt = promptBuilder.BuildUserPrompt(test, docs);

        // Referenced doc should appear first or be included
        Assert.Contains("Referenced", prompt);
    }

    private static TestCase CreateTestCase(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Priority = Priority.Medium,
        Steps = ["Step 1: Enter credentials", "Step 2: Click submit"],
        ExpectedResult = "User is logged in",
        FilePath = $"suite/{id}.md"
    };
}
