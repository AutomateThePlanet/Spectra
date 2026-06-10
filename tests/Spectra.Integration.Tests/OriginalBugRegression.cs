using System.Text.Json;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Extraction;
using Spectra.CLI.Commands.Analyze;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Generation;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Integration.Tests.Support;

namespace Spectra.Integration.Tests;

/// <summary>
/// Spec 052 Part B — named regression guards. Each test's <c>DisplayName</c> is the
/// ORIGINAL user-reported symptom, so a CI failure reads as "this user bug is back"
/// rather than an internal method name. Reverting the matching 047–051 fix fails the
/// matching test (SC-003).
/// </summary>
public sealed class OriginalBugRegression
{
    // ── 047: cache poisoning on parse failure ─────────────────────────────────

    [Fact(DisplayName = "Original bug: cache poisoning on parse failure")]
    public async Task ParseFailure_DoesNotPoisonCache()
    {
        var attempts = 0;
        var delay = new NoOpDelayProvider();

        var result = await AnalyzeHandler.ExtractWithRetryAsync(
            extractAttempt: _ =>
            {
                attempts++;
                return Task.FromResult(attempts == 1 ? ParseFail() : Extracted(2));
            },
            maxAttempts: 2,
            backoff: TimeSpan.FromMilliseconds(5),
            delayProvider: delay,
            ct: CancellationToken.None);

        // A parse failure is NEVER cacheable on its own — so the per-doc loop cannot
        // write its hash and poison the cache.
        Assert.False(ParseFail().IsCacheable);
        // The parse failure was retried (not silently cached), and the retry succeeded.
        Assert.Equal(2, attempts);
        Assert.True(result.IsCacheable);
        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
    }

    // ── 048: zero-criteria first index produced no warning ────────────────────

    [Fact(DisplayName = "Original bug: first big-project index produced zero criteria silently")]
    public void BigProjectFirstIndex_WarnsWhenZeroCriteria()
    {
        // Indexed many documents, extracted zero criteria → MUST warn, naming the fix.
        var warning = DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 541, criteriaExtractedTotal: 0);
        Assert.NotNull(warning);
        Assert.Contains("extract-criteria", warning);

        // No documents indexed → no warning (nothing to recover).
        Assert.Null(DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 0, criteriaExtractedTotal: 0));
        // Criteria found → no warning.
        Assert.Null(DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 541, criteriaExtractedTotal: 12));
    }

    // ── 050: extract-criteria on generation not working ───────────────────────

    [Fact(DisplayName = "Original bug: extract-criteria on generation not working")]
    public async Task ExtractCriteriaOnGeneration_PopulatesCriteriaField()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.SeedCriteriaAsync("checkout", ("AC-001", "Cart totals update on quantity change"));

        var (context, matched) = await ws.LoadCriteriaAsync("checkout");
        Assert.True(matched > 0, "criteria should match the suite by component");

        // Spec 059: the loaded criteria MUST flow into the compiled generation prompt (the
        // mandatory-mapping block) so the in-session agent maps them — the 050 guarantee, now on
        // the seam rather than the deleted in-process agent.
        var userPrompt = DescriptionPromptBuilder.Build("Checkout totals", null, "checkout", []);
        var prompt = PromptCompiler.Assemble(userPrompt, requestedCount: 1, criteriaContext: context);
        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", prompt);
        Assert.Contains("AC-001", prompt);
    }

    // ── 049: from-description test missing from index / different shape ────────

    [Fact(DisplayName = "Original bug: from-description test has different format and is missing from index")]
    public async Task FromDescriptionTest_AppearsInIndexWithSameShape()
    {
        await using var ws = new IntegrationWorkspace();
        var peer = TestFactory.Make("TC-001", "Existing peer", Priority.Medium,
            tags: ["regression"], component: "checkout", filePath: "checkout/TC-001.md");

        var produced = TestFactory.Make("TC-900", "From description", Priority.High,
            tags: ["smoke"], component: "checkout", filePath: "checkout/TC-900.md");

        // Spec 059: persist the produced test as the seam's ingest-tests does, then assert index parity.
        await ws.PersistAsync("checkout", new[] { produced }, new[] { peer, produced });

        var index = ws.ReadIndex("checkout");
        var entry = Assert.Single(index.Tests, e => e.Id == "TC-900");
        var peerEntry = Assert.Single(index.Tests, e => e.Id == "TC-001");

        // Same shape as a peer entry: id-bearing, .md file, lowercase priority, fields present.
        Assert.False(string.IsNullOrWhiteSpace(entry.Id));
        Assert.EndsWith(".md", entry.File);
        Assert.Equal(entry.Priority, entry.Priority.ToLowerInvariant());
        Assert.Equal("high", entry.Priority);
        // Structural parity: both entries expose the same kind of File path shape.
        Assert.Equal(Path.GetExtension(peerEntry.File), Path.GetExtension(entry.File));
    }

    // ── 051: high-priority filter from a suite returned the whole suite ────────

    [Fact(DisplayName = "Original bug: high priority filter from a suite returns whole suite")]
    public async Task HighPriorityFilter_FromSuite_ReturnsOnlyHighPriority()
    {
        await using var ws = new IntegrationWorkspace();
        await ws.PersistAsync("checkout", new[]
        {
            TestFactory.Make("TC-001", "High A", Priority.High, component: "checkout", filePath: "checkout/TC-001.md"),
            TestFactory.Make("TC-002", "Medium B", Priority.Medium, component: "checkout", filePath: "checkout/TC-002.md"),
            TestFactory.Make("TC-003", "High C", Priority.High, component: "checkout", filePath: "checkout/TC-003.md"),
            TestFactory.Make("TC-004", "Low D", Priority.Low, component: "checkout", filePath: "checkout/TC-004.md"),
        });

        var start = ws.BuildStartTool();
        var response = JsonDocument.Parse(await start.ExecuteAsync(
            JsonDocument.Parse("""{"suite":"checkout","priorities":["high"]}""").RootElement)).RootElement;

        var count = response.GetProperty("data").GetProperty("test_count").GetInt32();
        Assert.Equal(2, count);            // exactly the two high-priority tests
        Assert.NotEqual(4, count);         // NOT the whole suite (the original bug)
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static CriteriaExtractionResult Extracted(int count) =>
        new(ExtractionOutcome.Extracted,
            Enumerable.Range(1, count)
                .Select(i => new AcceptanceCriterion { Id = $"AC-{i:D3}", Text = $"Criterion {i}" })
                .ToList());

    private static CriteriaExtractionResult ParseFail() =>
        new(ExtractionOutcome.ParseFailure, Array.Empty<AcceptanceCriterion>());
}
