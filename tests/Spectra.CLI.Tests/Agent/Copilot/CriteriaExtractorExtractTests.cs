using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Extraction;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Tests.Agent.Copilot;

/// <summary>
/// Spec 047 test plan rows 6–7. Exercises <see cref="CriteriaExtractor.ExtractFromDocumentAsync"/>
/// for the two outcome paths that do NOT require an AI session (early-return paths).
/// Stubbing the Copilot SDK session itself is out of scope for unit tests; the
/// AI-response paths are covered exhaustively by <see cref="CriteriaExtractorParseTests"/>
/// via <see cref="CriteriaExtractor.ClassifyResponse"/>.
/// </summary>
public class CriteriaExtractorExtractTests
{
    private static CriteriaExtractor MakeExtractor() =>
        new(new SpectraProviderConfig { Name = "test", Model = "test-model", Enabled = true });

    [Fact]
    public async Task Extract_EmptyInputContent_ReturnsExtractedEmpty()
    {
        // Empty-input branch returns before any SDK call — cacheable so we
        // don't re-read an empty source file on every run.
        var extractor = MakeExtractor();

        var result = await extractor.ExtractFromDocumentAsync(
            documentPath: "docs/empty.md",
            documentContent: "   \n   \t   ",
            component: "empty",
            ct: CancellationToken.None);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.IsCacheable);
        Assert.Empty(result.Criteria);
    }

    [Fact]
    public void Extract_EmptyAiResponse_ReturnsEmptyResponse()
    {
        // Direct ClassifyResponse cover for the empty-AI-response branch.
        // Asserts that an empty/whitespace AI response is classified as
        // EmptyResponse (not cacheable) so the caller retries it.
        var result = CriteriaExtractor.ClassifyResponse(
            responseText: "   \n   ",
            source: "docs/silent.md",
            component: "silent");

        Assert.Equal(ExtractionOutcome.EmptyResponse, result.Outcome);
        Assert.False(result.IsCacheable);
        Assert.Empty(result.Criteria);
    }
}
