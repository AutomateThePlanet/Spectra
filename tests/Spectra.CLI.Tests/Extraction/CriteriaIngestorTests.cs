using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Extraction;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Extraction;

/// <summary>
/// Spec 054 — token-free unit tests for the model-free criteria ingest boundary.
/// Covers US1 (Extracted persists) and US3 (Empty/Parse fail loud, nothing persisted,
/// no cache poisoning). No model, no network.
/// </summary>
public sealed class CriteriaIngestorTests : IDisposable
{
    private readonly string _dir;

    public CriteriaIngestorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-ingest-crit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private static SpectraConfig Config() => SpectraConfig.Default; // CoverageConfig defaults: docs/criteria + index

    private const string GoodJson =
        "[{\"text\":\"System MUST validate IBAN\",\"rfc2119\":\"MUST\",\"priority\":\"high\",\"tags\":[\"payment\"]}]";

    // ---------- US1: classify Extracted + persist ----------

    [Fact]
    public void Classify_WellFormed_ReturnsExtracted()
    {
        var result = CriteriaIngestor.Classify(GoodJson, "docs/payment.md", "payment");

        Assert.True(result.IsSuccess);
        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Single(result.PersistedCriteria);
    }

    [Fact]
    public async Task IngestAsync_Extracted_WritesCriteriaFileAndIndex()
    {
        var ingestor = new CriteriaIngestor(Config());

        var result = await ingestor.IngestAsync(
            GoodJson, _dir, "docs/payment.md", component: "payment", docHash: "abc123", dryRun: false);

        Assert.True(result.IsSuccess);
        Assert.Single(result.PersistedCriteria);
        Assert.StartsWith("AC-PAYMENT-", result.PersistedCriteria[0].Id);

        var criteriaFile = Path.Combine(_dir, "docs/criteria", "payment.criteria.yaml");
        Assert.True(File.Exists(criteriaFile));

        var indexFile = Path.Combine(_dir, "docs/criteria/_criteria_index.yaml");
        Assert.True(File.Exists(indexFile));
        var index = await new CriteriaIndexReader().ReadAsync(indexFile);
        Assert.Contains(index.Sources, s => s.SourceDoc == "docs/payment.md" && s.DocHash == "abc123");
    }

    [Fact]
    public async Task IngestAsync_DryRun_PersistsNothing()
    {
        var ingestor = new CriteriaIngestor(Config());

        var result = await ingestor.IngestAsync(
            GoodJson, _dir, "docs/payment.md", "payment", "abc123", dryRun: true);

        Assert.True(result.IsSuccess); // classified Extracted...
        Assert.False(File.Exists(Path.Combine(_dir, "docs/criteria", "payment.criteria.yaml"))); // ...but wrote nothing
    }

    // ---------- US3: fail-loud, no persistence, no cache poisoning ----------

    [Fact]
    public void Classify_Malformed_ReturnsParseFailure_NeverThrows()
    {
        var result = CriteriaIngestor.Classify("this is not JSON at all", "docs/x.md", "x");

        Assert.False(result.IsSuccess);
        Assert.Equal(ExtractionOutcome.ParseFailure, result.Outcome);
        Assert.Empty(result.PersistedCriteria);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_Empty_ReturnsEmptyResponse_NeverThrows(string? content)
    {
        var result = CriteriaIngestor.Classify(content, "docs/x.md", "x");

        Assert.False(result.IsSuccess);
        Assert.Equal(ExtractionOutcome.EmptyResponse, result.Outcome);
    }

    [Fact]
    public async Task IngestAsync_NonCacheable_PersistsNothing_AndDoesNotPoisonIndex()
    {
        var ingestor = new CriteriaIngestor(Config());

        var parse = await ingestor.IngestAsync("garbage", _dir, "docs/payment.md", "payment", "abc", dryRun: false);
        Assert.False(parse.IsSuccess);
        Assert.Equal(ExtractionOutcome.ParseFailure, parse.Outcome);

        var empty = await ingestor.IngestAsync("   ", _dir, "docs/payment.md", "payment", "abc", dryRun: false);
        Assert.Equal(ExtractionOutcome.EmptyResponse, empty.Outcome);

        // No cache poisoning (FR-006): nothing written for either non-cacheable outcome.
        Assert.False(File.Exists(Path.Combine(_dir, "docs/criteria", "payment.criteria.yaml")));
        Assert.False(File.Exists(Path.Combine(_dir, "docs/criteria/_criteria_index.yaml")));
    }
}
