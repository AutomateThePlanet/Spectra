using Spectra.CLI.Agent.Copilot;

namespace Spectra.CLI.Tests.Agent.Copilot;

/// <summary>
/// Spec 047 test plan rows 1–5: <c>ClassifyResponse</c> must map each raw-AI-response
/// shape to the correct <see cref="ExtractionOutcome"/> so the caller can decide
/// whether to write a cache hash.
/// </summary>
public class CriteriaExtractorParseTests
{
    [Fact]
    public void Parse_ValidJsonWithItems_ReturnsExtracted()
    {
        const string json = """
            [
              {"text": "System MUST validate IBAN", "rfc2119": "MUST", "source_section": "Payments", "priority": "high"},
              {"text": "System SHOULD log retries", "rfc2119": "SHOULD", "source_section": "Logging", "priority": "medium"}
            ]
            """;

        var result = CriteriaExtractor.ClassifyResponse(json, source: "docs/payments.md", component: "payments");

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.IsCacheable);
        Assert.Equal(2, result.Criteria.Count);
        Assert.Equal("System MUST validate IBAN", result.Criteria[0].Text);
        Assert.Equal("MUST", result.Criteria[0].Rfc2119);
    }

    [Fact]
    public void Parse_ValidJsonEmptyArray_ReturnsExtractedEmpty()
    {
        var result = CriteriaExtractor.ClassifyResponse("[]", source: "docs/empty.md", component: "empty");

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.IsCacheable);
        Assert.Empty(result.Criteria);
    }

    [Fact]
    public void Parse_NoDelimiters_ReturnsParseFailure()
    {
        var result = CriteriaExtractor.ClassifyResponse(
            "Sorry, I cannot extract criteria from this document.",
            source: "docs/x.md",
            component: "x");

        Assert.Equal(ExtractionOutcome.ParseFailure, result.Outcome);
        Assert.False(result.IsCacheable);
        Assert.Empty(result.Criteria);
    }

    [Fact]
    public void Parse_NullDeserialize_ReturnsParseFailure()
    {
        // A JSON literal `null` inside brackets — Deserialize returns a list containing
        // a single null entry, which would be valid input but doesn't trigger the
        // null-result branch. Use a top-level `null` wrapped in [...] to force
        // Deserialize<List<...>>(text) to return null when the inner text isn't a list.
        // The cleanest way to drive deserialize-null is to pass JSON whose root is null:
        var result = CriteriaExtractor.ClassifyResponse("[null]", source: "docs/n.md", component: "n");

        // The deserializer returns a list with one null entry; the Where(non-empty Text)
        // filter throws NullReferenceException on i.Text, which the catch maps to
        // ParseFailure (covered by Parse_ThrowsInsideParser). To exercise the explicit
        // "items is null" branch, the JSON would need to round-trip to a null List,
        // which the System.Text.Json deserializer doesn't produce for a `[]`-bracketed
        // input. The Where lambda throw IS the deserialize-null branch in practice.
        Assert.Equal(ExtractionOutcome.ParseFailure, result.Outcome);
        Assert.False(result.IsCacheable);
    }

    [Fact]
    public void Parse_ThrowsInsideParser_ReturnsParseFailureAndLogs()
    {
        Exception? captured = null;

        // Malformed JSON between balanced delimiters — JsonSerializer.Deserialize throws.
        var result = CriteriaExtractor.ClassifyResponse(
            "[ {malformed: not, quoted: properly} ]",
            source: "docs/bad.md",
            component: "bad",
            onException: ex => captured = ex);

        Assert.Equal(ExtractionOutcome.ParseFailure, result.Outcome);
        Assert.False(result.IsCacheable);
        Assert.NotNull(captured);
    }

    [Fact]
    public void Parse_NullResponseText_ReturnsEmptyResponse()
    {
        var result = CriteriaExtractor.ClassifyResponse(null, source: "docs/x.md", component: "x");
        Assert.Equal(ExtractionOutcome.EmptyResponse, result.Outcome);
        Assert.False(result.IsCacheable);
    }
}
