#pragma warning disable CS0618 // RequirementDefinition is obsolete project-wide; the docs-index path still produces it.
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Commands.Docs;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Agent;

/// <summary>
/// Spec 054 (US4) — the legacy <see cref="RequirementsExtractor"/> is unified onto the typed
/// failure-semantics contract: it returns a typed outcome (no throw) sharing the
/// <see cref="ExtractionOutcome"/> enum + <c>IsCacheable</c> rule with the criteria path, and
/// <see cref="DocsIndexHandler.ExtractCriteriaLoopAsync"/> consumes it. Token-free.
/// </summary>
public sealed class RequirementsExtractorUnificationTests
{
    private const string GoodJson =
        "[{\"title\":\"User can reset password\",\"source\":\"docs/auth.md\",\"priority\":\"high\"}]";

    // ---------- ClassifyResponse: typed outcomes, never throws ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClassifyResponse_Empty_IsEmptyResponse(string? text)
    {
        var result = RequirementsExtractor.ClassifyResponse(text);

        Assert.Equal(ExtractionOutcome.EmptyResponse, result.Outcome);
        Assert.False(result.IsCacheable);
        Assert.Empty(result.Requirements);
    }

    [Fact]
    public void ClassifyResponse_NoArray_IsParseFailure_NeverThrows()
    {
        var result = RequirementsExtractor.ClassifyResponse("the model said: no requirements here");

        Assert.Equal(ExtractionOutcome.ParseFailure, result.Outcome);
        Assert.False(result.IsCacheable);
    }

    [Fact]
    public void ClassifyResponse_ValidArray_IsExtracted_AndCacheable()
    {
        var result = RequirementsExtractor.ClassifyResponse(GoodJson);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.IsCacheable);
        Assert.Single(result.Requirements);
        Assert.Equal("User can reset password", result.Requirements[0].Title);
    }

    [Fact]
    public void ClassifyResponse_EmptyJsonArray_IsExtracted_NotParseFailure()
    {
        // Parity with CriteriaExtractor.ClassifyResponse: a valid (but empty) array is a genuine
        // 'extracted nothing' — cacheable, not a failure.
        var result = RequirementsExtractor.ClassifyResponse("[]");

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.IsCacheable);
        Assert.Empty(result.Requirements);
    }

    [Fact]
    public void IsCacheable_MatchesCriteriaResultSemantics()
    {
        Assert.True(new RequirementsExtractionResult(ExtractionOutcome.Extracted, []).IsCacheable);
        Assert.False(new RequirementsExtractionResult(ExtractionOutcome.EmptyResponse, []).IsCacheable);
        Assert.False(new RequirementsExtractionResult(ExtractionOutcome.ParseFailure, []).IsCacheable);
    }

    // ---------- Loop consumes the typed outcome ----------

    private static DocumentEntry Doc(string path) => new()
    {
        Path = path, Title = path, SizeKb = 1, Headings = Array.Empty<string>(), Preview = string.Empty,
    };

    [Fact]
    public async Task Loop_NonCacheableOutcome_CountedAsFailed_NotAggregated_CorpusContinues()
    {
        var docs = new[] { Doc("docs/good.md"), Doc("docs/empty.md"), Doc("docs/good2.md") };
        var failures = new List<string>();

        var result = await DocsIndexHandler.ExtractCriteriaLoopAsync(
            documents: docs,
            existing: Array.Empty<RequirementDefinition>(),
            extractPerDoc: (doc, _) => Task.FromResult(doc.Path == "docs/empty.md"
                ? new RequirementsExtractionResult(ExtractionOutcome.EmptyResponse, [])
                : new RequirementsExtractionResult(ExtractionOutcome.Extracted,
                    new[] { new RequirementDefinition { Title = $"R from {doc.Path}", Priority = "medium" } })),
            perDocDeadline: TimeSpan.FromSeconds(5),
            onSlowDoc: _ => Assert.Fail("No doc should time out."),
            onDocFailure: (path, _) => failures.Add(path),
            ct: CancellationToken.None);

        Assert.Equal(2, result.Aggregated.Count);                 // only the two cacheable docs
        Assert.Single(result.FailedDocuments, "docs/empty.md");   // non-cacheable counted as failed
        Assert.Single(failures, "docs/empty.md");
        Assert.Empty(result.TimedOutDocuments);                   // corpus did not abort
    }
}
