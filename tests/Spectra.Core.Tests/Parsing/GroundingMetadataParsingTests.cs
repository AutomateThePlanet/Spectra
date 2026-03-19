using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class GroundingMetadataParsingTests
{
    private readonly TestCaseParser _parser = new();

    [Fact]
    public void Parse_WithGroundedMetadata_ParsesCorrectly()
    {
        const string markdown = """
            ---
            id: TC-101
            priority: high
            grounding:
              verdict: grounded
              score: 0.95
              generator: claude-sonnet-4
              critic: gemini-2.0-flash
              verified_at: 2026-03-19T10:30:00Z
            ---

            # Test with grounding

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(VerificationVerdict.Grounded, result.Value.Grounding.Verdict);
        Assert.Equal(0.95, result.Value.Grounding.Score);
        Assert.Equal("claude-sonnet-4", result.Value.Grounding.Generator);
        Assert.Equal("gemini-2.0-flash", result.Value.Grounding.Critic);
        Assert.Equal(2026, result.Value.Grounding.VerifiedAt.Year);
        Assert.Empty(result.Value.Grounding.UnverifiedClaims);
    }

    [Fact]
    public void Parse_WithPartialMetadata_IncludesUnverifiedClaims()
    {
        const string markdown = """
            ---
            id: TC-102
            priority: medium
            grounding:
              verdict: partial
              score: 0.72
              generator: gpt-4o
              critic: gemini-2.0-flash
              verified_at: 2026-03-19T11:00:00Z
              unverified_claims:
                - "Email sent within 5 minutes"
                - "Notification appears on mobile"
            ---

            # Test with partial grounding

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(VerificationVerdict.Partial, result.Value.Grounding.Verdict);
        Assert.Equal(0.72, result.Value.Grounding.Score);
        Assert.Equal(2, result.Value.Grounding.UnverifiedClaims.Count);
        Assert.Contains("Email sent within 5 minutes", result.Value.Grounding.UnverifiedClaims);
        Assert.Contains("Notification appears on mobile", result.Value.Grounding.UnverifiedClaims);
    }

    [Fact]
    public void Parse_WithHallucinatedMetadata_ParsesCorrectly()
    {
        const string markdown = """
            ---
            id: TC-103
            priority: low
            grounding:
              verdict: hallucinated
              score: 0.25
              generator: claude-sonnet-4
              critic: gpt-4o-mini
              verified_at: 2026-03-19T12:00:00Z
            ---

            # Hallucinated test

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(VerificationVerdict.Hallucinated, result.Value.Grounding.Verdict);
        Assert.Equal(0.25, result.Value.Grounding.Score);
    }

    [Fact]
    public void Parse_WithoutGrounding_GroundingIsNull()
    {
        const string markdown = """
            ---
            id: TC-104
            priority: medium
            ---

            # Test without grounding

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Grounding);
    }

    [Fact]
    public void Parse_WithIncompleteGrounding_GroundingIsNull()
    {
        // Missing required fields (generator, critic)
        const string markdown = """
            ---
            id: TC-105
            priority: medium
            grounding:
              verdict: grounded
              score: 0.9
            ---

            # Test with incomplete grounding

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Grounding);
    }

    [Fact]
    public void Parse_WithInvalidVerdict_GroundingIsNull()
    {
        const string markdown = """
            ---
            id: TC-106
            priority: medium
            grounding:
              verdict: invalid-verdict
              score: 0.5
              generator: gpt-4o
              critic: gemini-2.0-flash
              verified_at: 2026-03-19T10:00:00Z
            ---

            # Test with invalid verdict

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Grounding);
    }

    [Theory]
    [InlineData("grounded", VerificationVerdict.Grounded)]
    [InlineData("GROUNDED", VerificationVerdict.Grounded)]
    [InlineData("Grounded", VerificationVerdict.Grounded)]
    [InlineData("partial", VerificationVerdict.Partial)]
    [InlineData("PARTIAL", VerificationVerdict.Partial)]
    [InlineData("hallucinated", VerificationVerdict.Hallucinated)]
    [InlineData("HALLUCINATED", VerificationVerdict.Hallucinated)]
    public void Parse_VerdictIsCaseInsensitive(string verdictString, VerificationVerdict expected)
    {
        var markdown = $"""
            ---
            id: TC-107
            priority: medium
            grounding:
              verdict: {verdictString}
              score: 0.8
              generator: test-gen
              critic: test-critic
              verified_at: 2026-03-19T10:00:00Z
            ---

            # Test

            ## Expected Result

            Something
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(expected, result.Value.Grounding.Verdict);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 1.0)]
    public void Parse_ScoreIsClamped(double inputScore, double expectedScore)
    {
        var markdown = $"""
            ---
            id: TC-108
            priority: medium
            grounding:
              verdict: grounded
              score: {inputScore}
              generator: test-gen
              critic: test-critic
              verified_at: 2026-03-19T10:00:00Z
            ---

            # Test

            ## Expected Result

            Something
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(expectedScore, result.Value.Grounding.Score);
    }

    [Fact]
    public void Parse_WithInvalidVerifiedAt_UsesMinValue()
    {
        const string markdown = """
            ---
            id: TC-109
            priority: medium
            grounding:
              verdict: grounded
              score: 0.9
              generator: test-gen
              critic: test-critic
              verified_at: not-a-date
            ---

            # Test

            ## Expected Result

            Something
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(DateTimeOffset.MinValue, result.Value.Grounding.VerifiedAt);
    }

    [Fact]
    public void Parse_GroundingWithOtherFields_AllFieldsParsed()
    {
        const string markdown = """
            ---
            id: TC-110
            priority: high
            tags: [smoke, api]
            component: auth
            source_refs: [docs/auth.md]
            grounding:
              verdict: grounded
              score: 0.98
              generator: claude-sonnet-4
              critic: gemini-2.0-flash
              verified_at: 2026-03-19T14:00:00Z
            ---

            # Complete test with grounding

            ## Preconditions

            User must be logged in

            ## Steps

            1. Do step one
            2. Do step two

            ## Expected Result

            Something happens
            """;

        var result = _parser.Parse(markdown, "test.md");

        Assert.True(result.IsSuccess);

        // Verify other fields still parse
        Assert.Equal("TC-110", result.Value.Id);
        Assert.Equal(Priority.High, result.Value.Priority);
        Assert.Contains("smoke", result.Value.Tags);
        Assert.Equal("auth", result.Value.Component);
        Assert.Contains("docs/auth.md", result.Value.SourceRefs);
        Assert.Equal(2, result.Value.Steps.Count);

        // Verify grounding
        Assert.NotNull(result.Value.Grounding);
        Assert.Equal(VerificationVerdict.Grounded, result.Value.Grounding.Verdict);
        Assert.Equal(0.98, result.Value.Grounding.Score);
    }
}
