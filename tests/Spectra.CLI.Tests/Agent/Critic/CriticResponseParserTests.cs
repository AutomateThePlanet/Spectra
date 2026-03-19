using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Agent.Critic;

public class CriticResponseParserTests
{
    private readonly CriticResponseParser _parser = new();

    [Fact]
    public void Parse_ValidGroundedResponse()
    {
        var json = """
            {
              "verdict": "grounded",
              "score": 0.95,
              "findings": [
                {
                  "element": "Step 1",
                  "claim": "Navigate to login",
                  "status": "grounded",
                  "evidence": "Section 2.1: Login page access"
                }
              ]
            }
            """;

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, result.Verdict);
        Assert.Equal(0.95, result.Score);
        Assert.Single(result.Findings);
        Assert.Equal(FindingStatus.Grounded, result.Findings[0].Status);
    }

    [Fact]
    public void Parse_ValidPartialResponse()
    {
        var json = """
            {
              "verdict": "partial",
              "score": 0.72,
              "findings": [
                {
                  "element": "Step 3",
                  "claim": "Email arrives within 5 minutes",
                  "status": "unverified",
                  "reason": "No time specified in documentation"
                }
              ]
            }
            """;

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Partial, result.Verdict);
        Assert.Equal(0.72, result.Score);
        Assert.Equal(FindingStatus.Unverified, result.Findings[0].Status);
        Assert.NotNull(result.Findings[0].Reason);
    }

    [Fact]
    public void Parse_ValidHallucinatedResponse()
    {
        var json = """
            {
              "verdict": "hallucinated",
              "score": 0.25,
              "findings": [
                {
                  "element": "Expected Result",
                  "claim": "Fraud API returns score",
                  "status": "hallucinated",
                  "reason": "No fraud API in documentation"
                }
              ]
            }
            """;

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Hallucinated, result.Verdict);
        Assert.Equal(0.25, result.Score);
        Assert.Equal(FindingStatus.Hallucinated, result.Findings[0].Status);
    }

    [Fact]
    public void Parse_JsonInMarkdownCodeBlock()
    {
        var response = """
            ```json
            {
              "verdict": "grounded",
              "score": 0.9,
              "findings": []
            }
            ```
            """;

        var result = _parser.Parse(response, "test-model", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationVerdict.Grounded, result.Verdict);
    }

    [Fact]
    public void Parse_EmptyResponse_ReturnsError()
    {
        var result = _parser.Parse("", "test-model", TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("Empty response", result.Errors[0]);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsError()
    {
        var result = _parser.Parse("not valid json", "test-model", TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Parse_MissingVerdict_DefaultsToPartial()
    {
        var json = """
            {
              "score": 0.5,
              "findings": []
            }
            """;

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.Equal(VerificationVerdict.Partial, result.Verdict);
    }

    [Fact]
    public void Parse_MissingScore_DefaultsToHalf()
    {
        var json = """
            {
              "verdict": "grounded",
              "findings": []
            }
            """;

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.Equal(0.5, result.Score);
    }

    [Fact]
    public void Parse_ScoreOutOfRange_IsClamped()
    {
        var json = """
            {
              "verdict": "grounded",
              "score": 1.5,
              "findings": []
            }
            """;

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.Equal(1.0, result.Score);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(2.0, 1.0)]
    public void Parse_ClampScoreToValidRange(double input, double expected)
    {
        var json = $@"{{""verdict"": ""grounded"", ""score"": {input}, ""findings"": []}}";

        var result = _parser.Parse(json, "test-model", TimeSpan.FromSeconds(1));

        Assert.Equal(expected, result.Score);
    }

    [Fact]
    public void Parse_SetsModelName()
    {
        var json = """{"verdict": "grounded", "score": 0.9, "findings": []}""";

        var result = _parser.Parse(json, "gemini-2.0-flash", TimeSpan.FromSeconds(1));

        Assert.Equal("gemini-2.0-flash", result.CriticModel);
    }

    [Fact]
    public void Parse_SetsDuration()
    {
        var json = """{"verdict": "grounded", "score": 0.9, "findings": []}""";
        var duration = TimeSpan.FromMilliseconds(1500);

        var result = _parser.Parse(json, "test-model", duration);

        Assert.Equal(duration, result.Duration);
    }
}
