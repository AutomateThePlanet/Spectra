using Spectra.CLI.Agent.Critic;
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
    [Fact]
    public void VerificationPipeline_PromptBuilder_BuildsValidPrompt()
    {
        var promptBuilder = new CriticPromptBuilder();
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
    public void VerificationPipeline_ResponseParser_ParsesGroundedVerdict()
    {
        var parser = new CriticResponseParser();
        var response = """
            {
              "verdict": "grounded",
              "score": 0.95,
              "findings": [
                {
                  "element": "Step 1",
                  "claim": "User can log in",
                  "status": "grounded",
                  "evidence": "Section 2.1 describes login"
                }
              ]
            }
            """;

        var result = parser.Parse(response, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, result.Verdict);
        Assert.Equal(0.95, result.Score);
        Assert.Single(result.Findings);
    }

    [Fact]
    public void VerificationPipeline_ResponseParser_ParsesPartialVerdict()
    {
        var parser = new CriticResponseParser();
        var response = """
            {
              "verdict": "partial",
              "score": 0.72,
              "findings": [
                {
                  "element": "Step 1",
                  "claim": "User sees welcome message",
                  "status": "grounded",
                  "evidence": "Welcome text shown"
                },
                {
                  "element": "Step 2",
                  "claim": "Email sent within 5 minutes",
                  "status": "unverified",
                  "reason": "No time specified in documentation"
                }
              ]
            }
            """;

        var result = parser.Parse(response, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Partial, result.Verdict);
        Assert.Equal(2, result.Findings.Count);
        Assert.Contains(result.Findings, f => f.Status == FindingStatus.Grounded);
        Assert.Contains(result.Findings, f => f.Status == FindingStatus.Unverified);
    }

    [Fact]
    public void VerificationPipeline_ResponseParser_ParsesHallucinatedVerdict()
    {
        var parser = new CriticResponseParser();
        var response = """
            {
              "verdict": "hallucinated",
              "score": 0.25,
              "findings": [
                {
                  "element": "Expected Result",
                  "claim": "Fraud detection API returns score",
                  "status": "hallucinated",
                  "reason": "No fraud API exists in documentation"
                }
              ]
            }
            """;

        var result = parser.Parse(response, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Hallucinated, result.Verdict);
        Assert.Single(result.Findings);
        Assert.Equal(FindingStatus.Hallucinated, result.Findings[0].Status);
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
    public void VerificationPipeline_CriticFactory_ValidatesProviders()
    {
        Assert.True(CriticFactory.IsSupported("google"));
        Assert.True(CriticFactory.IsSupported("openai"));
        Assert.True(CriticFactory.IsSupported("anthropic"));
        Assert.True(CriticFactory.IsSupported("github-models"));
        Assert.True(CriticFactory.IsSupported("azure-openai"));
        Assert.True(CriticFactory.IsSupported("azure-anthropic"));
        Assert.False(CriticFactory.IsSupported("unknown"));
    }

    [Fact]
    public void VerificationPipeline_PromptBuilder_HandlesEmptyDocs()
    {
        var promptBuilder = new CriticPromptBuilder();
        var test = CreateTestCase("TC-001", "Test");
        var docs = new List<SourceDocument>();

        var prompt = promptBuilder.BuildUserPrompt(test, docs);

        Assert.Contains("No relevant documentation provided", prompt);
    }

    [Fact]
    public void VerificationPipeline_PromptBuilder_TruncatesLargeDocs()
    {
        var promptBuilder = new CriticPromptBuilder();
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
        var promptBuilder = new CriticPromptBuilder();
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

    [Fact]
    public void VerificationPipeline_ResponseParser_HandlesMarkdownWrappedJson()
    {
        var parser = new CriticResponseParser();
        var response = """
            Here is my analysis:

            ```json
            {
              "verdict": "grounded",
              "score": 0.9,
              "findings": []
            }
            ```

            Hope this helps!
            """;

        var result = parser.Parse(response, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, result.Verdict);
    }

    [Fact]
    public void VerificationPipeline_ResponseParser_HandlesMalformedResponse()
    {
        var parser = new CriticResponseParser();
        var response = "This is not valid JSON at all";

        var result = parser.Parse(response, "test-model", TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void VerificationPipeline_EndToEnd_GroundedFlow()
    {
        // Test the complete flow from prompt through result
        var promptBuilder = new CriticPromptBuilder();
        var parser = new CriticResponseParser();

        var test = CreateTestCase("TC-001", "Verify user login");
        var docs = new List<SourceDocument>
        {
            new()
            {
                Path = "docs/auth.md",
                Title = "Authentication",
                Content = "Users log in with email and password. System validates credentials and returns session token."
            }
        };

        // Build prompts
        var systemPrompt = promptBuilder.BuildSystemPrompt();
        var userPrompt = promptBuilder.BuildUserPrompt(test, docs);

        Assert.NotEmpty(systemPrompt);
        Assert.NotEmpty(userPrompt);

        // Simulate critic response
        var criticResponse = """
            {
              "verdict": "grounded",
              "score": 0.92,
              "findings": [
                {
                  "element": "Step 1",
                  "claim": "User enters email",
                  "status": "grounded",
                  "evidence": "Users log in with email and password"
                }
              ]
            }
            """;

        // Parse response
        var result = parser.Parse(criticResponse, "gemini-2.0-flash", TimeSpan.FromSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, result.Verdict);

        // Convert to metadata
        var metadata = result.ToMetadata("gpt-4o");

        Assert.Equal("gpt-4o", metadata.Generator);
        Assert.Equal("gemini-2.0-flash", metadata.Critic);
        Assert.True(metadata.IsValid());
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
